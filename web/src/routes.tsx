import { createBrowserRouter } from 'react-router-dom'
import { RequiereAdmin, RequiereSesion } from '@/auth/guardias'
import RootLayout from '@/layouts/RootLayout'
import LoginPage from '@/pages/LoginPage'
import NotFoundPage from '@/pages/NotFoundPage'
import GrupoPage from '@/pages/admin/GrupoPage'
import GruposPage from '@/pages/admin/GruposPage'
import PartidaPage from '@/pages/panel/PartidaPage'
import SesionesPage from '@/pages/panel/SesionesPage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <RootLayout />,
    errorElement: <NotFoundPage />,
    children: [
      { path: 'login', element: <LoginPage /> },

      // Panel del adulto responsable.
      {
        element: <RequiereSesion />,
        children: [
          { index: true, element: <SesionesPage /> },
          { path: 'partidas/:id', element: <PartidaPage /> },

          // Panel de administracion: ademas de sesion, exige is_admin.
          {
            path: 'admin',
            element: <RequiereAdmin />,
            children: [
              { path: 'grupos', element: <GruposPage /> },
              { path: 'grupos/:id', element: <GrupoPage /> },
            ],
          },
        ],
      },

      { path: '*', element: <NotFoundPage /> },
    ],
  },
])
