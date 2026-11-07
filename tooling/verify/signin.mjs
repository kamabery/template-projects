// Signs in to a deployed front end as its QA user, in a real browser, then asks the API who that is.
//
//   node signin.mjs --url <front end> --me <the API's /me> --user <qa user> --token-file <path>
//   (the password on stdin, never as an argument)
//
// Passes only when the browser is back on the front end showing Sign out, /me answers 200 with the
// token the app obtained, and 401 without it. The token goes to --token-file so live.ps1 can try it on
// another preview's API; it is never printed.
import { writeFileSync } from 'node:fs'
import { chromium } from 'playwright'

const arg = (name) => {
  const i = process.argv.indexOf(`--${name}`)
  return i > 0 ? process.argv[i + 1] : undefined
}
const url = arg('url')
const meUrl = arg('me')
const user = arg('user')
const tokenFile = arg('token-file')
if (!url || !meUrl || !user || !tokenFile) {
  console.log('error: --url, --me, --user and --token-file are all required')
  process.exit(2)
}

let password = ''
for await (const chunk of process.stdin) password += chunk
password = password.trim()
if (!password) {
  console.log('error: no password on stdin')
  process.exit(2)
}

const deadline = Date.now() + 180_000
const origin = new URL(url).origin
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))
const browser = await chromium.launch()
const page = await browser.newPage()

async function stop(why) {
  const body = await page.locator('body').innerText({ timeout: 2000 }).catch(() => '')
  console.log(`error: ${why}`)
  console.log(`at: ${page.url().split('?')[0]}`)
  console.log(`page: ${body.replace(/\s+/g, ' ').replaceAll(password, '***').slice(0, 300)}`)
  await browser.close()
  process.exit(1)
}

const shown = (locator) => locator.first().isVisible().catch(() => false)

try {
  // A container scaled to zero, or a WebAssembly app, can take a while to offer Sign in.
  const signIn = page.getByRole('button', { name: /^sign in$/i })
  for (;;) {
    await page.goto(url, { timeout: 60_000 }).catch(() => {})
    if (await signIn.first().waitFor({ timeout: 20_000 }).then(() => true, () => false)) break
    if (Date.now() > deadline) await stop('Sign in never appeared')
  }
  await signIn.first().click()
  console.log('step: clicked Sign in')

  let userDone = false
  let passwordDone = false
  let leftOrigin = false
  while (!(leftOrigin && passwordDone && new URL(page.url()).origin === origin)) {
    if (Date.now() > deadline) await stop('timed out on the identity provider')
    const here = new URL(page.url())
    if (here.origin !== origin) leftOrigin = true
    const text = await page.locator('body').innerText({ timeout: 1000 }).catch(() => '')
    const code = text.match(/AADSTS\d+[^\n]*/)
    if (code) await stop(code[0])
    if (here.searchParams.get('error')) await stop(`${here.searchParams.get('error')}: ${here.searchParams.get('error_description') ?? ''}`)

    const userBox = page.locator('input[type=email], input[name=loginfmt], input[name=username]').filter({ visible: true })
    const passwordBox = page.locator('input[type=password]').filter({ visible: true })
    const submit = page.locator('button[type=submit], input[type=submit]').filter({ visible: true })
    const no = page.getByRole('button', { name: /^no$/i })
    if (!userDone && (await shown(userBox))) {
      await userBox.first().fill(user)
      userDone = true
      console.log('step: entered the user')
      if (!(await shown(passwordBox))) await submit.first().click().catch(() => page.keyboard.press('Enter'))
    } else if (!passwordDone && (await shown(passwordBox))) {
      await passwordBox.first().fill(password)
      passwordDone = true
      console.log('step: entered the password')
      await submit.first().click().catch(() => page.keyboard.press('Enter'))
    } else if (passwordDone && (await shown(no))) {
      await no.first().click()
      console.log('step: answered Stay signed in')
    }
    await sleep(500)
  }
  console.log('step: back on the front end')

  const signOut = page.getByRole('button', { name: /^sign out$/i })
  while (!(await shown(signOut))) {
    if (Date.now() > deadline) await stop('back on the front end, but Sign out never appeared')
    await sleep(500)
  }
  console.log('step: Sign out is showing')

  // oidc-client-ts and Blazor's OIDC library both keep the user as JSON holding access_token.
  const token = await page.evaluate(() => {
    for (const store of [sessionStorage, localStorage]) {
      for (let i = 0; i < store.length; i++) {
        try {
          const value = JSON.parse(store.getItem(store.key(i)))
          if (value?.access_token) return value.access_token
        } catch {}
      }
    }
    return null
  })
  if (!token) await stop('no access token in the browser after signing in')
  writeFileSync(tokenFile, token)

  const withToken = await fetch(meUrl, { headers: { Authorization: `Bearer ${token}` } })
  const without = await fetch(meUrl)
  console.log(`me with token: ${withToken.status}`)
  console.log(`me without token: ${without.status}`)
  if (withToken.status !== 200) await stop(`/me answered ${withToken.status} with the token`)
  if (without.status !== 401) await stop(`/me answered ${without.status} without a token`)
  await browser.close()
  console.log('result: pass')
} catch (e) {
  await stop(String(e?.message ?? e).split('\n')[0])
}
