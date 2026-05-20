import { defineStore } from 'pinia'
import { ref } from 'vue'
import { parseResume, scoreMatch, generateInterviewGuide } from '@/api/resume-ai'
import type { ParseResult, MatchScoreResult, InterviewGuideResult } from '@/api/resume-ai'

export const useResumeAiStore = defineStore('resumeAi', () => {
  const parseCache = ref<Map<string, ParseResult>>(new Map())
  const matchCache = ref<Map<string, MatchScoreResult>>(new Map())
  const guideCache = ref<Map<string, InterviewGuideResult>>(new Map())

  const parseLoading = ref(false)
  const matchLoading = ref(false)
  const guideLoading = ref(false)

  function cacheKey(resumeId: number, jobId?: number) {
    return `${resumeId}_${jobId ?? 0}`
  }

  async function fetchParse(resumeId: number): Promise<ParseResult> {
    const key = cacheKey(resumeId)
    if (parseCache.value.has(key)) return parseCache.value.get(key)!
    parseLoading.value = true
    try {
      const res = await parseResume({ resumeId })
      const data = (res as any)?.data || res
      parseCache.value.set(key, data as ParseResult)
      return data as ParseResult
    } finally { parseLoading.value = false }
  }

  async function fetchMatch(resumeId: number, jobId?: number): Promise<MatchScoreResult> {
    const key = cacheKey(resumeId, jobId)
    if (matchCache.value.has(key)) return matchCache.value.get(key)!
    matchLoading.value = true
    try {
      const res = await scoreMatch({ resumeId, jobId })
      const data = (res as any)?.data || res
      matchCache.value.set(key, data as MatchScoreResult)
      return data as MatchScoreResult
    } finally { matchLoading.value = false }
  }

  async function fetchGuide(resumeId: number, jobId?: number): Promise<InterviewGuideResult> {
    const key = cacheKey(resumeId, jobId)
    if (guideCache.value.has(key)) return guideCache.value.get(key)!
    guideLoading.value = true
    try {
      const res = await generateInterviewGuide({ resumeId, jobId })
      const data = (res as any)?.data || res
      guideCache.value.set(key, data as InterviewGuideResult)
      return data as InterviewGuideResult
    } finally { guideLoading.value = false }
  }

  function clearCache() {
    parseCache.value.clear()
    matchCache.value.clear()
    guideCache.value.clear()
  }

  return { parseLoading, matchLoading, guideLoading, fetchParse, fetchMatch, fetchGuide, clearCache }
})
