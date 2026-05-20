/**
 * AI response safe parser - handles MiniMax M2.7 think tags
 */

const THINK_REGEX = /<think>[\s\S]*?<\/think>/gi

/** Extract first complete JSON using stack-based matching */
function extractFirstJSON(str: string): any {
  try { return JSON.parse(str) } catch { /* retry */ }

  const startBrace = str.indexOf('{')
  const startBracket = str.indexOf('[')
  let startIdx = -1
  if (startBrace === -1) startIdx = startBracket
  else if (startBracket === -1) startIdx = startBrace
  else startIdx = Math.min(startBrace, startBracket)

  if (startIdx === -1) return null

  const openChar = str[startIdx]
  const closeChar = openChar === '{' ? '}' : ']'
  let depth = 0
  let inStr = false
  let escape = false

  for (let i = startIdx; i < str.length; i++) {
    const ch = str[i]
    if (escape) { escape = false; continue }
    if (ch === '\\' && inStr) { escape = true; continue }
    if (ch === '"') { inStr = !inStr; continue }
    if (inStr) continue

    if (ch === openChar) depth++
    if (ch === closeChar) {
      depth--
      if (depth === 0) {
        try { return JSON.parse(str.substring(startIdx, i + 1)) } catch { return null }
      }
    }
  }
  return null
}

/** Filter MiniMax M2.7 think tags and safely extract JSON */
export function parseAIResponse(res: any): any {
  if (res && typeof res === 'object') return res
  if (!res || typeof res !== 'string') return null

  const cleaned = res.replace(THINK_REGEX, '').trim()

  try { return JSON.parse(cleaned) } catch { /* retry */ }

  return extractFirstJSON(cleaned)
}
