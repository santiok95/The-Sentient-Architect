'use client'

import { cn } from '@/lib/utils'

interface MarkdownProps {
  content: string
  className?: string
}

/**
 * Lightweight markdown renderer — no external dependencies.
 * Handles the subset the AI assistant actually produces:
 * headings, bold, italic, inline code, fenced code blocks,
 * ordered/unordered lists, blockquotes, horizontal rules, and paragraphs.
 */
export function Markdown({ content, className }: MarkdownProps) {
  const nodes = parse(content)
  return (
    <div className={cn('markdown', className)}>
      {nodes.map((node, i) => renderNode(node, i))}
    </div>
  )
}

// ─── Types ────────────────────────────────────────────────────────────────────

type Node =
  | { type: 'h1' | 'h2' | 'h3'; inline: InlineNode[] }
  | { type: 'p'; inline: InlineNode[] }
  | { type: 'ul'; items: InlineNode[][] }
  | { type: 'ol'; items: InlineNode[][] }
  | { type: 'blockquote'; inline: InlineNode[] }
  | { type: 'code'; lang: string; text: string }
  | { type: 'hr' }

type InlineNode =
  | { kind: 'text'; text: string }
  | { kind: 'bold'; text: string }
  | { kind: 'italic'; text: string }
  | { kind: 'bolditalic'; text: string }
  | { kind: 'code'; text: string }
  | { kind: 'link'; text: string; href: string }

// ─── Block parser ─────────────────────────────────────────────────────────────

function parse(raw: string): Node[] {
  const lines = raw.split('\n')
  const nodes: Node[] = []
  let i = 0

  while (i < lines.length) {
    const line = lines[i]

    // Fenced code block
    if (line.startsWith('```')) {
      const lang = line.slice(3).trim()
      const codeLines: string[] = []
      i++
      while (i < lines.length && !lines[i].startsWith('```')) {
        codeLines.push(lines[i])
        i++
      }
      i++ // consume closing ```
      nodes.push({ type: 'code', lang, text: codeLines.join('\n') })
      continue
    }

    // Horizontal rule
    if (/^[-*_]{3,}\s*$/.test(line)) {
      nodes.push({ type: 'hr' })
      i++
      continue
    }

    // Headings
    const h3 = line.match(/^### (.+)/)
    if (h3) { nodes.push({ type: 'h3', inline: parseInline(h3[1]) }); i++; continue }
    const h2 = line.match(/^## (.+)/)
    if (h2) { nodes.push({ type: 'h2', inline: parseInline(h2[1]) }); i++; continue }
    const h1 = line.match(/^# (.+)/)
    if (h1) { nodes.push({ type: 'h1', inline: parseInline(h1[1]) }); i++; continue }

    // Blockquote
    if (line.startsWith('> ')) {
      nodes.push({ type: 'blockquote', inline: parseInline(line.slice(2)) })
      i++
      continue
    }

    // Unordered list — collect consecutive items
    if (/^[-*+] /.test(line)) {
      const items: InlineNode[][] = []
      while (i < lines.length && /^[-*+] /.test(lines[i])) {
        items.push(parseInline(lines[i].replace(/^[-*+] /, '')))
        i++
      }
      nodes.push({ type: 'ul', items })
      continue
    }

    // Ordered list
    if (/^\d+\. /.test(line)) {
      const items: InlineNode[][] = []
      while (i < lines.length && /^\d+\. /.test(lines[i])) {
        items.push(parseInline(lines[i].replace(/^\d+\. /, '')))
        i++
      }
      nodes.push({ type: 'ol', items })
      continue
    }

    // Blank line — skip
    if (line.trim() === '') { i++; continue }

    // Paragraph
    nodes.push({ type: 'p', inline: parseInline(line) })
    i++
  }

  return nodes
}

// ─── Inline parser ────────────────────────────────────────────────────────────

function parseInline(text: string): InlineNode[] {
  const nodes: InlineNode[] = []
  // Order matters: bolditalic > bold > italic > code > link
  const pattern = /(\*\*\*(.+?)\*\*\*|\*\*(.+?)\*\*|\*(.+?)\*|`(.+?)`|\[(.+?)\]\((.+?)\))/g
  let last = 0
  let match: RegExpExecArray | null

  while ((match = pattern.exec(text)) !== null) {
    if (match.index > last) {
      nodes.push({ kind: 'text', text: text.slice(last, match.index) })
    }

    if (match[2]) nodes.push({ kind: 'bolditalic', text: match[2] })
    else if (match[3]) nodes.push({ kind: 'bold', text: match[3] })
    else if (match[4]) nodes.push({ kind: 'italic', text: match[4] })
    else if (match[5]) nodes.push({ kind: 'code', text: match[5] })
    else if (match[6] && match[7]) nodes.push({ kind: 'link', text: match[6], href: match[7] })

    last = match.index + match[0].length
  }

  if (last < text.length) {
    nodes.push({ kind: 'text', text: text.slice(last) })
  }

  return nodes.length ? nodes : [{ kind: 'text', text }]
}

// ─── Renderers ────────────────────────────────────────────────────────────────

function renderInline(nodes: InlineNode[], key: number) {
  return nodes.map((n, i) => {
    const k = `${key}-${i}`
    switch (n.kind) {
      case 'text': return <span key={k}>{n.text}</span>
      case 'bold': return <strong key={k} className="font-semibold">{n.text}</strong>
      case 'italic': return <em key={k} className="italic">{n.text}</em>
      case 'bolditalic': return <strong key={k} className="font-semibold italic">{n.text}</strong>
      case 'code': return (
        <code key={k} className="rounded bg-muted px-1.5 py-0.5 font-mono text-xs text-foreground">
          {n.text}
        </code>
      )
      case 'link': return (
        <a key={k} href={n.href} target="_blank" rel="noopener noreferrer"
          className="text-primary underline underline-offset-2 hover:text-primary/80">
          {n.text}
        </a>
      )
    }
  })
}

function renderNode(node: Node, key: number) {
  switch (node.type) {
    case 'h1': return (
      <h1 key={key} className="mt-4 mb-2 text-base font-bold tracking-tight text-foreground">
        {renderInline(node.inline, key)}
      </h1>
    )
    case 'h2': return (
      <h2 key={key} className="mt-4 mb-1.5 text-sm font-semibold text-foreground border-b border-border pb-1">
        {renderInline(node.inline, key)}
      </h2>
    )
    case 'h3': return (
      <h3 key={key} className="mt-3 mb-1 text-sm font-semibold text-foreground/90">
        {renderInline(node.inline, key)}
      </h3>
    )
    case 'p': return (
      <p key={key} className="my-1.5 leading-relaxed">
        {renderInline(node.inline, key)}
      </p>
    )
    case 'ul': return (
      <ul key={key} className="my-2 space-y-1 pl-4">
        {node.items.map((item, i) => (
          <li key={i} className="flex items-start gap-2">
            <span className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-primary/70" />
            <span className="leading-relaxed">{renderInline(item, i)}</span>
          </li>
        ))}
      </ul>
    )
    case 'ol': return (
      <ol key={key} className="my-2 space-y-1 pl-4">
        {node.items.map((item, i) => (
          <li key={i} className="flex items-start gap-2">
            <span className="mt-0.5 shrink-0 font-mono text-xs text-primary/70 font-semibold w-4">{i + 1}.</span>
            <span className="leading-relaxed">{renderInline(item, i)}</span>
          </li>
        ))}
      </ol>
    )
    case 'blockquote': return (
      <blockquote key={key} className="my-2 border-l-2 border-primary/40 pl-3 text-muted-foreground italic">
        {renderInline(node.inline, key)}
      </blockquote>
    )
    case 'code': return (
      <div key={key} className="my-3 overflow-hidden rounded-lg border border-border bg-muted/50">
        {node.lang && (
          <div className="flex items-center gap-1.5 border-b border-border bg-muted px-3 py-1.5">
            <span className="h-2 w-2 rounded-full bg-destructive/60" />
            <span className="h-2 w-2 rounded-full bg-amber-400/60" />
            <span className="h-2 w-2 rounded-full bg-green-400/60" />
            <span className="ml-2 font-mono text-[10px] text-muted-foreground">{node.lang}</span>
          </div>
        )}
        <pre className="overflow-x-auto p-4">
          <code className="font-mono text-xs leading-relaxed text-foreground">
            {node.text}
          </code>
        </pre>
      </div>
    )
    case 'hr': return (
      <hr key={key} className="my-3 border-border" />
    )
  }
}
