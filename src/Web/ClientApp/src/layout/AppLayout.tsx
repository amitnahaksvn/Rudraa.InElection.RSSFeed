import { useState, type ReactNode } from 'react';
import { Link, useLocation } from 'react-router-dom';
import AppBar from '@mui/material/AppBar';
import Toolbar from '@mui/material/Toolbar';
import IconButton from '@mui/material/IconButton';
import Typography from '@mui/material/Typography';
import Drawer from '@mui/material/Drawer';
import List from '@mui/material/List';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Box from '@mui/material/Box';
import useMediaQuery from '@mui/material/useMediaQuery';
import MenuIcon from '@mui/icons-material/Menu';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import HubIcon from '@mui/icons-material/Hub';

const DRAWER_WIDTH = 240;

// Every nav destination the sidebar shows - a single array to extend when more admin pages are
// added later, rather than hand-wiring a new ListItemButton each time.
const NAV_ITEMS = [
  { label: 'Error Monitor', path: '/errors', icon: <ErrorOutlineIcon /> },
  { label: 'Provider Management', path: '/providers', icon: <HubIcon /> },
];

export function AppLayout({ children }: { children: ReactNode }) {
  const isDesktop = useMediaQuery('(min-width: 900px)');
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();
  const currentNavItem = NAV_ITEMS.find((item) => location.pathname.startsWith(item.path));

  const drawerContent = (
    <>
      <Toolbar>
        <Typography variant="subtitle1" fontWeight={700} noWrap>
          Rudraa Admin
        </Typography>
      </Toolbar>
      <List>
        {NAV_ITEMS.map((item) => (
          <ListItemButton
            key={item.path}
            component={Link}
            to={item.path}
            selected={location.pathname.startsWith(item.path)}
            onClick={() => setMobileOpen(false)}
          >
            <ListItemIcon>{item.icon}</ListItemIcon>
            <ListItemText primary={item.label} />
          </ListItemButton>
        ))}
      </List>
    </>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar
        position="fixed"
        color="default"
        elevation={0}
        sx={{
          width: { md: `calc(100% - ${DRAWER_WIDTH}px)` },
          ml: { md: `${DRAWER_WIDTH}px` },
          borderBottom: 1,
          borderColor: 'divider',
        }}
      >
        <Toolbar>
          {!isDesktop && (
            <IconButton edge="start" onClick={() => setMobileOpen(true)} sx={{ mr: 2 }}>
              <MenuIcon />
            </IconButton>
          )}
          <Typography variant="h6" noWrap>
            {currentNavItem?.label ?? 'Rudraa Admin'}
          </Typography>
        </Toolbar>
      </AppBar>

      <Box component="nav" sx={{ width: { md: DRAWER_WIDTH }, flexShrink: { md: 0 } }}>
        <Drawer
          variant={isDesktop ? 'permanent' : 'temporary'}
          open={isDesktop || mobileOpen}
          onClose={() => setMobileOpen(false)}
          ModalProps={{ keepMounted: true }}
          sx={{
            '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box' },
          }}
        >
          {drawerContent}
        </Drawer>
      </Box>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          width: { md: `calc(100% - ${DRAWER_WIDTH}px)` },
          display: 'flex',
          flexDirection: 'column',
        }}
      >
        <Toolbar />
        <Box sx={{ flexGrow: 1, p: { xs: 1.5, sm: 3 }, overflow: 'auto' }}>{children}</Box>
      </Box>
    </Box>
  );
}
