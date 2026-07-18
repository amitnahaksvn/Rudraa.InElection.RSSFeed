import { useMemo, useState } from 'react';
import JoditEditor from 'jodit-react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import ToggleButton from '@mui/material/ToggleButton';
import { useColorScheme } from '@mui/material/styles';
import { sanitizeRichText } from './sanitizeRichText';

// Jodit's own toolbar swaps to a *different, larger* default button list below its internal
// "large" breakpoint (buttonsMD/buttonsSM/buttonsXS) rather than just collapsing the same list -
// leaving those unset meant every dialog wide enough to still count as "medium" showed Jodit's
// full ~32-button stock toolbar (image/video/table/AI commands/spellcheck/...) instead of this
// curated one, however narrow this editor actually renders. Setting all four to the same array
// keeps the toolbar consistent - and its own overflow ("...") menu, from toolbarAdaptive, is what
// handles genuinely narrow (phone-width) dialogs, not a smaller button set.
const BUTTONS = [
  'bold',
  'italic',
  'underline',
  'strikethrough',
  '|',
  'ul',
  'ol',
  '|',
  'paragraph',
  'align',
  'outdent',
  'indent',
  '|',
  'font',
  'fontsize',
  'brush',
  '|',
  'link',
  '|',
  'undo',
  'redo',
];

interface RichTextEditorProps {
  label: string;
  defaultValue?: string;
  onChange: (html: string) => void;
  placeholder?: string;
  disabled?: boolean;
  minHeight?: number;
  required?: boolean;
}

// A Jodit-based rich text editor (xdsoft.net/jodit, MIT-licensed, free) - a full Summernote-style
// toolbar (font/size/color/alignment/lists/link/etc.) out of the box, with a Write/Preview toggle.
// Jodit owns its whole editing surface internally rather than relying on the browser's deprecated
// document.execCommand the way a hand-rolled contentEditable editor did (twice - once directly,
// once via a from-scratch build on top of it), so it doesn't share those reliability problems.
// `value` is only used to seed initial content, same "effectively uncontrolled" pattern as the
// two prior implementations - jodit-react doesn't re-push it into the editor after every
// keystroke, so there's no fighting over cursor position. The Preview tab reuses sanitizeRichText,
// the same sanitizer every other render path (HistoryTimeline) uses before trusting this HTML.
export function RichTextEditor({
  label,
  defaultValue = '',
  onChange,
  placeholder,
  disabled,
  minHeight = 240,
  required,
}: RichTextEditorProps) {
  const [mode, setMode] = useState<'write' | 'preview'>('write');
  const [html, setHtml] = useState(defaultValue);

  // Jodit ships its own light-mode-only default styling, unaware of this app's dark/light
  // theme - the editing surface and toolbar stayed light even when the rest of the app (and the
  // text color it inherited) switched to dark, so typed text could end up white-on-white or
  // black-on-black. Jodit does bundle a real `dark` theme (a `jodit_theme_dark` class with its
  // own contrast-correct CSS), so this just needs to be kept in sync with MUI's resolved mode
  // rather than left on Jodit's own default.
  const { mode: colorMode, systemMode } = useColorScheme();
  const resolvedColorMode = colorMode === 'system' ? systemMode : colorMode;
  const isDarkMode = resolvedColorMode === 'dark';

  const config = useMemo(
    () => ({
      readonly: !!disabled,
      placeholder: placeholder ?? '',
      minHeight,
      theme: isDarkMode ? 'dark' : 'default',
      // Adaptive (Jodit's default): collapses overflowing buttons into a "..." menu once the
      // toolbar is narrower than its buttons need - matters on a phone-width dialog, where all of
      // BUTTONS can't fit; a wide desktop dialog has room to show every button anyway.
      toolbarAdaptive: true,
      showXPathInStatusbar: false,
      showCharsCounter: false,
      showWordsCounter: false,
      showPoweredByJodit: false,
      statusbar: false,
      buttons: BUTTONS,
      buttonsMD: BUTTONS,
      buttonsSM: BUTTONS,
      buttonsXS: BUTTONS,
    }),
    [disabled, placeholder, minHeight, isDarkMode],
  );

  const isEmpty = !html.replace(/<[^>]*>/g, '').trim();

  return (
    <Box>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 0.5 }}>
        <Typography variant="caption" color="text.secondary">
          {label}
          {required && ' *'}
        </Typography>
        <ToggleButtonGroup
          size="small"
          exclusive
          value={mode}
          onChange={(_, next) => next && setMode(next)}
          sx={{ height: 24 }}
        >
          <ToggleButton value="write" sx={{ px: 1, py: 0, fontSize: 11 }}>
            Write
          </ToggleButton>
          <ToggleButton value="preview" sx={{ px: 1, py: 0, fontSize: 11 }}>
            Preview
          </ToggleButton>
        </ToggleButtonGroup>
      </Stack>
      <Box sx={{ border: 1, borderColor: 'divider', borderRadius: 1, overflow: 'hidden' }}>
        {/* Kept mounted (just hidden), not conditionally rendered, so switching to Preview and
            back never re-creates the underlying Jodit instance. */}
        <Box sx={{ display: mode === 'write' ? 'block' : 'none', fontSize: 14 }}>
          <JoditEditor
            value={defaultValue}
            config={config}
            onChange={(next) => {
              setHtml(next);
              onChange(next);
            }}
          />
        </Box>

        {mode === 'preview' && (
          <Box sx={{ minHeight, maxHeight: 420, overflow: 'auto', p: 1.25, fontSize: 14, '& ul, & ol': { pl: 3, my: 0.5 } }}>
            {isEmpty ? (
              <Typography variant="body2" color="text.disabled">
                Nothing to preview yet.
              </Typography>
            ) : (
              <div dangerouslySetInnerHTML={{ __html: sanitizeRichText(html) }} />
            )}
          </Box>
        )}
      </Box>
    </Box>
  );
}
