import Snackbar from '@mui/material/Snackbar';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import { usePendingTransitions, TRANSITION_DELAY_MS } from './PendingTransitionContext';

export function PendingTransitionToasts() {
  const { transitions } = usePendingTransitions();

  return (
    <>
      {transitions.map((transition, index) => (
        <Snackbar
          key={transition.key}
          open
          anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
          sx={{ mb: index * 8 }}
        >
          <Box
            sx={{
              minWidth: { xs: 0, sm: 320 },
              maxWidth: { xs: 'calc(100vw - 32px)', sm: 420 },
              borderRadius: 1.5,
              overflow: 'hidden',
              bgcolor: 'grey.900',
              color: 'common.white',
              boxShadow: 4,
            }}
          >
            <Box sx={{ px: 2, py: 1.25 }}>
              <Typography variant="body2">{transition.message}</Typography>
            </Box>
            <Box sx={{ height: 3, bgcolor: 'rgba(255,255,255,0.2)' }}>
              <Box
                sx={{
                  height: '100%',
                  bgcolor: 'primary.main',
                  width: '100%',
                  animation: `pendingTransitionDrain ${TRANSITION_DELAY_MS}ms linear forwards`,
                  '@keyframes pendingTransitionDrain': {
                    from: { width: '100%' },
                    to: { width: '0%' },
                  },
                }}
              />
            </Box>
          </Box>
        </Snackbar>
      ))}
    </>
  );
}
