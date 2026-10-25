import { createBrowserRouter, Navigate } from 'react-router-dom'
import { RequiereAdmin, RequierePadre, RequiereSesion } from '@/auth/guardias'
import RootLayout from '@/layouts/RootLayout'
import LoginPage from '@/pages/LoginPage'
import InvitacionPage from '@/pages/InvitacionPage'
import NotFoundPage from '@/pages/NotFoundPage'
import GrupoPage from '@/pages/admin/GrupoPage'
import GruposPage from '@/pages/admin/GruposPage'
import ReporteGrupoPage from '@/pages/admin/ReporteGrupoPage'
import PartidaPage from '@/pages/panel/PartidaPage'
import SesionesPage from '@/pages/panel/SesionesPage'
import ReporteNinoPage from '@/pages/panel/ReporteNinoPage'
import BienvenidaPage from '@/pages/BienvenidaPage'
import RecuperarContrasenaPage from '@/pages/RecuperarContrasenaPage'

export const rutas = [{
  path: '/', element: <RootLayout />, errorElement: <NotFoundPage />,
  children: [
    { index: true, element: <BienvenidaPage /> },
    { path: 'login', element: <LoginPage /> },
    { path: 'registro', element: <LoginPage registro /> },
    { path: 'recuperar-contrasena', element: <RecuperarContrasenaPage /> },
    { path: 'invitacion', element: <InvitacionPage /> },
    { element: <RequiereSesion />, children: [
      { element: <RequierePadre />, children: [
        { path: 'panel', element: <SesionesPage /> },
        { path: 'reportes', element: <Navigate to="/" replace /> },
        { path: 'reportes/:id', element: <ReporteNinoPage /> },
        { path: 'partidas/:id', element: <PartidaPage /> },
      ] },
      // Gestión y reportes grupales exclusivos del tutor administrador (profesor).
      // El servicio real también debe validar rol y pertenencia del grupo.
      { path: 'admin', element: <RequiereAdmin />, children: [
        { index: true, element: <Navigate to="/admin/grupos" replace /> },
        { path: 'grupos', element: <GruposPage /> },
        { path: 'grupos/:id', element: <GrupoPage /> },
        { path: 'grupos/:id/reporte', element: <ReporteGrupoPage /> },
      ] },
    ] },
    { path: '*', element: <NotFoundPage /> },
  ],
}]
export const router = createBrowserRouter(rutas)
