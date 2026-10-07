import { create } from 'zustand'

interface CartUiState {
  open: boolean
  setOpen: (open: boolean) => void
}

export const useCartUi = create<CartUiState>((set) => ({ open: false, setOpen: (open) => set({ open }) }))
