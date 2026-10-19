import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router-dom'
import { ProveedorSesion } from '@/auth/ProveedorSesion'
import { router } from '@/routes'
import './index.css'
import { demoPeriodo } from '@/lib/periodo'

async function iniciar() {
  const Proveedor = import.meta.env.DEV && demoPeriodo ? (await import('@/demo/ProveedorPeriodoDemo')).ProveedorPeriodoDemo : ProveedorSesion
  createRoot(document.getElementById('root')!).render(<StrictMode><Proveedor><RouterProvider router={router} /></Proveedor></StrictMode>)
}
void iniciar()
