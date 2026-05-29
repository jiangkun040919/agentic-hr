/** 格式化薪资：15000 → 15K, 18(K值) → 18K */
export const formatSalary = (value?: number | null): string => {
  if (value == null) return ''
  // 如果值 <= 100，说明已经是 K 值，直接使用
  const k = value <= 100 ? value : value / 1000
  return (k % 1 === 0 ? k.toFixed(0) : k.toFixed(1).replace(/\.0$/, '')) + 'K'
}
