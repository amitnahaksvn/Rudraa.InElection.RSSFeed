import { useEffect, useState, type ReactNode } from 'react';
import { EditorContent, useEditor } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Underline from '@tiptap/extension-underline';
import Placeholder from '@tiptap/extension-placeholder';
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

interface ToolbarAction {
  label: string;
  icon: ReactNode;
  isActive: (editor: NonNullable<ReturnType<typeof useEditor>>) => boolean;
  run: (editor: NonNullable<ReturnType<typeof useEditor>>) => void;
}

const TOOLBAR_ACTIONS: ToolbarAction[] = [
  {
    label: 'Bold',
    icon: <FormatBoldIcon fontSize="small" />,
    isActive: (editor) => editor.isActive('bold'),
    run: (editor) => editor.chain().focus().toggleBold().run(),
  },
  {
    label: 'Italic',
    icon: <FormatItalicIcon fontSize="small" />,
    isActive: (editor) => editor.isActive('italic'),
    run: (editor) => editor.chain().focus().toggleItalic().run(),
  },
  {
    label: 'Underline',
    icon: <FormatUnderlinedIcon fontSize="small" />,
    isActive: (editor) => editor.isActive('underline'),
    run: (editor) => editor.chain().focus().toggleUnderline().run(),
  },
  {
    label: 'Bulleted list',
    icon: <FormatListBulletedIcon fontSize="small" />,
    isActive: (editor) => editor.isActive('bulletList'),
    run: (editor) => editor.chain().focus().toggleBulletList().run(),
  },
  {
    label: 'Numbered list',
    icon: <FormatListNumberedIcon fontSize="small" />,
    isActive: (editor) => editor.isActive('orderedList'),
    run: (editor) => editor.chain().focus().toggleOrderedList().run(),
  },
];

// A Tiptap-based rich text editor (bold/italic/underline/lists) with a Write/Preview toggle.
// Tiptap (MIT-licensed, github.com/ueberdosis/tiptap) owns its own document model instead of
// relying on the browser's contentEditable + document.execCommand, which is deprecated and
// notoriously inconsistent across browsers - a hand-rolled contentEditable editor built directly
// on it kept losing keystrokes and formatting state. The Preview tab reuses sanitizeRichText, the
// same sanitizer every other render path (HistoryTimeline) uses before trusting this HTML.
export function RichTextEditor({
  label,
  defaultValue = '',
  onChange,
  placeholder,
  disabled,
  minHeight = 96,
  required,
}: RichTextEditorProps) {
  const [mode, setMode] = useState<'write' | 'preview'>('write');

  const editor = useEditor({
    extensions: [
      StarterKit.configure({ heading: false, blockquote: false, codeBlock: false, horizontalRule: false }),
      Underline,
      Placeholder.configure({ placeholder: placeholder ?? '' }),
    ],
    content: defaultValue,
    editable: !disabled,
    onUpdate: ({ editor }) => onChange(editor.getHTML()),
    editorProps: {
      attributes: {
        style: `min-height:${minHeight}px`,
      },
    },
  });

  useEffect(() => {
    editor?.setEditable(!disabled);
  }, [disabled, editor]);

  const html = editor?.getHTML() ?? defaultValue;
  const isEmpty = editor?.isEmpty ?? true;

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
            back never re-creates the underlying Tiptap editor instance. */}
        <Box sx={{ display: mode === 'write' ? 'block' : 'none' }}>
          <Stack direction="row" sx={{ borderBottom: 1, borderColor: 'divider', px: 0.5, py: 0.25, bgcolor: 'action.hover' }}>
            {TOOLBAR_ACTIONS.map((action) => (
              <Tooltip key={action.label} title={action.label}>
                <span>
                  <IconButton
                    size="small"
                    disabled={disabled || !editor}
                    color={editor && action.isActive(editor) ? 'primary' : 'default'}
                    onMouseDown={(e) => e.preventDefault()}
                    onClick={() => editor && action.run(editor)}
                    aria-label={action.label}
                  >
                    {action.icon}
                  </IconButton>
                </span>
              </Tooltip>
            ))}
          </Stack>
          <Box
            sx={{
              minHeight,
              maxHeight: 260,
              overflow: 'auto',
              p: 1.25,
              fontSize: 14,
              cursor: 'text',
              '& .tiptap': { outline: 'none' },
              '& ul, & ol': { pl: 3, my: 0.5 },
              '& p': { m: 0 },
              '& p.is-editor-empty:first-of-type::before': {
                content: 'attr(data-placeholder)',
                color: 'text.disabled',
                float: 'left',
                height: 0,
                pointerEvents: 'none',
              },
            }}
            onClick={() => editor?.chain().focus().run()}
          >
            <EditorContent editor={editor} />
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
