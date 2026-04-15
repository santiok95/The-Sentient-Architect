import { clsx, type ClassValue } from "clsx"
import { twMerge } from "tailwind-merge"

/** Merge Tailwind classes safely (handles conditional classes + deduplication). */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/** Format an ISO date string into a localized short date. */
export function formatDate(iso: string, locale = 'es-AR'): string {
  return new Intl.DateTimeFormat(locale, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(new Date(iso))
}

/** Truncate a string to `max` chars, appending '…' if longer. */
export function truncate(text: string, max = 80): string {
  if (text.length <= max) return text
  return `${text.slice(0, max - 1)}…`
}

/** Pluralize a noun based on count. */
export function pluralize(count: number, singular: string, plural: string): string {
  return count === 1 ? singular : plural
}
