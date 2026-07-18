import DOMPurify from 'dompurify';

// Comment/Description are rich text (HTML) authored by whoever resolved/commented on an error -
// untrusted the moment it's rendered back to a *different* viewer (HistoryTimeline, or this
// editor's own Preview tab), so every render path sanitizes with DOMPurify first rather than
// trusting what's in the database or what the editor produced. The allowlist matches Jodit's
// toolbar: lists/basic emphasis plus links, font/size/color, strikethrough, and headings/quotes
// (the "paragraph" format-block button) - DOMPurify sanitizes the `style` attribute's contents
// itself (stripping things like javascript: URLs or expression()), so allowing it isn't an XSS
// hole on its own. h1-h6/blockquote were missing here even though the toolbar could already
// produce them, so applying a heading and saving it silently dropped the tag (and its styling) on
// the very next render - not visible as literal text, just formatting that quietly vanished.
const ALLOWED_TAGS = [
  'b',
  'i',
  'u',
  'strong',
  'em',
  'ul',
  'ol',
  'li',
  'br',
  'div',
  'span',
  'p',
  'a',
  'font',
  'strike',
  's',
  'h1',
  'h2',
  'h3',
  'h4',
  'h5',
  'h6',
  'blockquote',
];
const ALLOWED_ATTR = ['href', 'target', 'rel', 'style', 'color', 'size', 'face'];

export function sanitizeRichText(html: string): string {
  return DOMPurify.sanitize(html, { ALLOWED_TAGS, ALLOWED_ATTR });
}
