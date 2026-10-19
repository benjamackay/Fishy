import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router-dom'
import { ProveedorSesion } from '@/auth/ProveedorSesion'
import { router } from '@/routes'
import { modoDemo } from '@/lib/modoDemo'
import './index.css'

async function iniciar() {
  const Proveedor = import.meta.env.DEV && modoDemo ? (await import('@/demo/ProveedorDemo')).ProveedorDemo : ProveedorSesion
  createRoot(document.getElementById('root')!).render(
    <StrictMode><Proveedor><RouterProvider router={router} /></Proveedor></StrictMode>,
  )
}
void iniciar()
