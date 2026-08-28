import { Store, useStore } from '@tanstack/react-store'

/**
 * The count lives outside the component on purpose. /counter exists to prove the
 * store-to-render path works with no HTTP and no configuration involved, which
 * only means something if the state is not component-local.
 */
export const counterStore = new Store({ count: 0 })

export function increment() {
  counterStore.setState((state) => ({ count: state.count + 1 }))
}

export function useCount(): number {
  return useStore(counterStore, (state) => state.count)
}
