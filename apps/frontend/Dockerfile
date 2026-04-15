# Build stage
FROM node:22-alpine AS builder

WORKDIR /app

# Copy package files
COPY package.json package-lock.json* yarn.lock* pnpm-lock.yaml* ./

# Install dependencies with lightningcss binary for musl
RUN npm install --omit=optional && npm install --save-optional lightningcss-linux-x64-musl

# Copy source code
COPY . .

# NEXT_PUBLIC_* vars must be declared BEFORE npm run build —
# Next.js inlines them into the bundle at compile time.
ARG NEXT_PUBLIC_API_URL
ARG NEXT_PUBLIC_API_MOCK=false
ENV NEXT_PUBLIC_API_URL=${NEXT_PUBLIC_API_URL}
ENV NEXT_PUBLIC_API_MOCK=${NEXT_PUBLIC_API_MOCK}

# Build Next.js app
RUN npm run build

# Runtime stage
FROM node:22-alpine

WORKDIR /app

# Set environment to production
ENV NODE_ENV=production

# Copy only necessary files from builder
COPY --from=builder /app/.next ./.next
COPY --from=builder /app/node_modules ./node_modules
COPY --from=builder /app/package.json ./package.json
COPY --from=builder /app/public ./public

# Expose port (Next.js default)
EXPOSE 3000

# Health check (allow redirects)
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD node -e "require('http').get('http://localhost:3000', (r) => {if (![200, 307].includes(r.statusCode)) throw new Error(r.statusCode)})"

# Start Next.js
CMD ["npm", "start"]
