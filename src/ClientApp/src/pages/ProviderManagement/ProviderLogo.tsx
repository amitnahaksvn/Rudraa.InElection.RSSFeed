import { useState } from 'react';
import Avatar from '@mui/material/Avatar';
import { getAvatarColor, getFaviconUrl, getInitials } from '../../utils/providerVisuals';

export function ProviderLogo({ name, domain, size = 44 }: { name: string; domain?: string; size?: number }) {
  const [imgFailed, setImgFailed] = useState(false);
  const showImage = Boolean(domain) && !imgFailed;

  return (
    <Avatar
      variant="rounded"
      src={showImage ? getFaviconUrl(domain!) : undefined}
      slotProps={{ img: { onError: () => setImgFailed(true) } }}
      sx={{
        width: size,
        height: size,
        bgcolor: getAvatarColor(name),
        fontWeight: 700,
        fontSize: size * 0.36,
        flexShrink: 0,
      }}
    >
      {getInitials(name)}
    </Avatar>
  );
}
