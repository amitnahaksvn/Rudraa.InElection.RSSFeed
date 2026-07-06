import { useState, useRef, type ReactNode } from 'react';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import FormatBoldIcon from '@mui/icons-material/FormatBold';
import FormatItalicIcon from '@mui/icons-material/FormatItalic';
import FormatUnderlinedIcon from '@mui/icons-material/FormatUnderlined';
import FormatListBulletedIcon from '@mui/icons-material/FormatListBulleted';
import FormatListNumberedIcon from '@mui/icons-material/FormatListNumbered';

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

// A minimal contentEditable-based rich text editor (bold/italic/underline/lists) - the
// comment/description fields only need light formatting, so this avoids pulling in a full WYSIWYG
// library. Deliberately uncontrolled (like a plain <input defaultValue>, not value): contentEditable
// fights back hard against being treated as a controlled component, so the caller resets it by
// changing this component's `key` (e.g. when a dialog re-opens) rather than pushing new HTML in.
// The HTML it produces is untrusted the moment it's saved and viewed by someone else - callers
// that render it back out (HistoryTimeline) must sanitize with DOMPurify before doing so.
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

  const handleInput = () => {
    const html = editorRef.current?.innerHTML ?? '';
    setIsEmpty((editorRef.current?.textContent ?? '').trim() === '');
    onChange(html);
  };

  const runCommand = (command: string) => {
    editorRef.current?.focus();
    document.execCommand(command);
    handleInput();
  };

  return (
    <Box>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
        {label}
        {required && ' *'}
      </Typography>
      <Box sx={{ border: 1, borderColor: 'divider', borderRadius: 1, overflow: 'hidden' }}>
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
            dangerouslySetInnerHTML={{ __html: defaultValue }}
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
    </Box>
  );
}
