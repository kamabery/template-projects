// The development service worker, and deliberately inert.
//
// Offline caching needs service-worker-assets.js, which the build generates on
// publish only; under `dotnet run` the DevServer also serves unfingerprinted
// assets under hot reload, and caching those is a reliable way to spend an
// afternoon staring at stale output. So this worker caches nothing.
//
// Registering it is still worth doing: a registered worker plus a manifest is
// what makes the app installable, so the install affordance works in dev too.
// The real worker is wwwroot/service-worker.published.js, swapped in at publish
// time by the ServiceWorker item in the .csproj.
self.addEventListener('fetch', () => { });
