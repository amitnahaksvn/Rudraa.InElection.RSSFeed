import DOMPurify from 'dompurify';

// Comment/Description are rich text (HTML) authored by whoever resolved/commented on an error -
// untrusted the moment it's rendered back to a *different* viewer (HistoryTimeline, or this
// editor's own Preview tab), so every render path sanitizes with DOMPurify first rather than
// trusting what's in the database or what execCommand produced.
const ALLOWED_TAGS = ['b', 'i', 'u', 'strong', 'em', 'ul', 'ol', 'li', 'br', 'div', 'span', 'p'];

export function sanitizeRichText(html: string): string {
  return DOMPurify.sanitize(html, { ALLOWED_TAGS });
}
