import { useEffect, useRef, useState, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import ToggleButton from '@mui/material/ToggleButton';
import FormatBoldIcon from '@mui/icons-material/FormatBold';
import FormatItalicIcon from '@mui/icons-material/FormatItalic';
import FormatUnderlinedIcon from '@mui/icons-material/FormatUnderlined';
import FormatListBulletedIcon from '@mui/icons-material/FormatListBulleted';
import FormatListNumberedIcon from '@mui/icons-material/FormatListNumbered';
import { sanitizeRichText } from './sanitizeRichText';

interface RichTextEditorProps {
  label: string;
  defaultValue?: string;
  onChange: (html: string) => void;
  placeholder?: string;
  disabled?: boolean;
  minHeight?: number;
  required?: boolean;
}

const TOOLBAR_ACTIONS: { command: string; label: string; icon: ReactNode }[] = [
  { command: 'bold', label: 'Bold', icon: <FormatBoldIcon fontSize="small" /> },
  { command: 'italic', label: 'Italic', icon: <FormatItalicIcon fontSize="small" /> },
  { command: 'underline', label: 'Underline', icon: <FormatUnderlinedIcon fontSize="small" /> },
  { command: 'insertUnorderedList', label: 'Bulleted list', icon: <FormatListBulletedIcon fontSize="small" /> },
  { command: 'insertOrderedList', label: 'Numbered list', icon: <FormatListNumberedIcon fontSize="small" /> },
];

// A minimal contentEditable-based rich text editor (bold/italic/underline/lists) with a
// Write/Preview toggle - avoids pulling in a full WYSIWYG library for what's just light
// formatting. Deliberately uncontrolled (like a plain <input defaultValue>, not value):
// contentEditable fights back hard against being treated as a controlled component, so the caller
// resets it by changing this component's `key` (e.g. when a dialog re-opens) rather than pushing
// new HTML in on every render.
//
// The initial HTML is seeded into the DOM exactly once, imperatively (the effect below), rather
// than via a `dangerouslySetInnerHTML` prop in the JSX - React's diffing for that prop compares
// the *wrapper object's identity*, not the `__html` string inside it, so a fresh `{ __html: ... }`
// literal on every render (which is unavoidable if it's inline in JSX) made React reset the
// node's content back to the original empty string after every single keystroke, which is exactly
// what made typing appear broken. The Preview tab reuses sanitizeRichText, the same sanitizer
// every other render path (HistoryTimeline) uses before trusting this HTML.
export function RichTextEditor({
  label,
  defaultValue = '',
  onChange,
  placeholder,
  disabled,
  minHeight = 96,
  required,
}: RichTextEditorProps) {
  const editorRef = useRef<HTMLDivElement | null>(null);
  const [isEmpty, setIsEmpty] = useState(!defaultValue || !defaultValue.replace(/<[^>]*>/g, '').trim());
  const [mode, setMode] = useState<'write' | 'preview'>('write');
  const [html, setHtml] = useState(defaultValue);

  useEffect(() => {
    if (editorRef.current) {
      editorRef.current.innerHTML = defaultValue;
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleInput = () => {
    const nextHtml = editorRef.current?.innerHTML ?? '';
    setHtml(nextHtml);
    setIsEmpty((editorRef.current?.textContent ?? '').trim() === '');
    onChange(nextHtml);
  };

  const runCommand = (command: string) => {
    editorRef.current?.focus();
    document.execCommand(command);
    handleInput();
  };

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
        {/* Kept mounted (just hidden) rather than conditionally rendered, so the imperative
            content-seeding effect above never has to re-run against a freshly (re)mounted node. */}
        <Box sx={{ display: mode === 'write' ? 'block' : 'none' }}>
          <Stack direction="row" sx={{ borderBottom: 1, borderColor: 'divider', px: 0.5, py: 0.25, bgcolor: 'action.hover' }}>
            {TOOLBAR_ACTIONS.map(({ command, label: actionLabel, icon }) => (
              <Tooltip key={command} title={actionLabel}>
                <span>
                  <IconButton
                    size="small"
                    disabled={disabled}
                    onMouseDown={(e) => e.preventDefault()}
                    onClick={() => runCommand(command)}
                    aria-label={actionLabel}
                  >
                    {icon}
                  </IconButton>
                </span>
              </Tooltip>
            ))}
          </Stack>
          <Box sx={{ position: 'relative' }}>
            {isEmpty && placeholder && (
              <Typography
                variant="body2"
                color="text.disabled"
                sx={{ position: 'absolute', top: 10, left: 13, pointerEvents: 'none' }}
              >
                {placeholder}
              </Typography>
            )}
            <Box
              ref={editorRef}
              contentEditable={!disabled}
              suppressContentEditableWarning
              onInput={handleInput}
              sx={{
                minHeight,
                maxHeight: 260,
                overflow: 'auto',
                p: 1.25,
                fontSize: 14,
                outline: 'none',
                '& ul, & ol': { pl: 3, my: 0.5 },
              }}
            />
          </Box>
        </Box>

        {mode === 'preview' && (
          <Box sx={{ minHeight, maxHeight: 260, overflow: 'auto', p: 1.25, fontSize: 14, '& ul, & ol': { pl: 3, my: 0.5 } }}>
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
