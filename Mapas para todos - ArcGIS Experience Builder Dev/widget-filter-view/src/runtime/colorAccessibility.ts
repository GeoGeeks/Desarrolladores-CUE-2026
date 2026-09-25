export type CvdType = 'protanopia' | 'deuteranopia' | 'tritanopia'
type Lab = [number, number, number]

// Matrices de Machado, Oliveira & Fernandes (2009)
const MACHADO_MATRICES: Record<CvdType, number[][]> = {
  protanopia: [
    [0.152286, 1.052583, -0.204868],
    [0.114503, 0.786281, 0.099216],
    [-0.003882, -0.048116, 1.051998]
  ],
  deuteranopia: [
    [0.367322, 0.860646, -0.227968],
    [0.280085, 0.672501, 0.047413],
    [-0.01182, 0.04294, 0.968881]
  ],
  tritanopia: [
    [1.255528, -0.076749, -0.178779],
    [-0.078411, 0.930809, 0.147602],
    [0.004733, 0.691367, 0.3039]
  ]
}

const XYZ_MATRIX = [
  [0.4124564, 0.3575761, 0.1804375],
  [0.2126729, 0.7151522, 0.0721750],
  [0.0193339, 0.1191920, 0.9503041]
]
const WHITE: [number, number, number] = [0.95047, 1.0, 1.08883]

function hexToSrgb01(hex: string): [number, number, number] {
  const h = hex.replace('#', '')
  return [
    parseInt(h.substring(0, 2), 16) / 255,
    parseInt(h.substring(2, 4), 16) / 255,
    parseInt(h.substring(4, 6), 16) / 255
  ]
}

function srgbToLinear(c: number): number {
  return c <= 0.04045 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4)
}

function linearToSrgb(c: number): number {
  const clamped = Math.min(1, Math.max(0, c))
  return clamped <= 0.0031308 ? clamped * 12.92 : 1.055 * Math.pow(clamped, 1 / 2.4) - 0.055
}

function applyMatrix(rgb: [number, number, number], m: number[][]): [number, number, number] {
  return [
    m[0][0] * rgb[0] + m[0][1] * rgb[1] + m[0][2] * rgb[2],
    m[1][0] * rgb[0] + m[1][1] * rgb[1] + m[1][2] * rgb[2],
    m[2][0] * rgb[0] + m[2][1] * rgb[1] + m[2][2] * rgb[2]
  ]
}

function simulateCvd(srgb: [number, number, number], type: CvdType): [number, number, number] {
  const linear = srgb.map(srgbToLinear) as [number, number, number]
  const simLinear = applyMatrix(linear, MACHADO_MATRICES[type])
  return simLinear.map(linearToSrgb) as [number, number, number]
}

function fLab(t: number): number {
  const delta = 6 / 29
  return t > Math.pow(delta, 3) ? Math.cbrt(t) : t / (3 * delta * delta) + 4 / 29
}

function srgbToLab(srgb: [number, number, number]): Lab {
  const linear = srgb.map(srgbToLinear) as [number, number, number]
  const xyz = applyMatrix(linear, XYZ_MATRIX)
  const fx = fLab(xyz[0] / WHITE[0])
  const fy = fLab(xyz[1] / WHITE[1])
  const fz = fLab(xyz[2] / WHITE[2])
  return [116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)]
}

function labDistance(a: Lab, b: Lab): number {
  return Math.sqrt((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2)
}

export interface CvdRisk {
  type: CvdType
  minDistance: number
  atRisk: boolean
}

const SAFE_THRESHOLD = 12

export function analyzeColors(hexColors: string[]): CvdRisk[] {
  const types: CvdType[] = ['protanopia', 'deuteranopia', 'tritanopia']
  return types.map(type => {
    if (hexColors.length < 2) return { type, minDistance: Infinity, atRisk: false }
    const labs = hexColors.map(h => srgbToLab(simulateCvd(hexToSrgb01(h), type)))
    let minDistance = Infinity
    for (let i = 0; i < labs.length; i++) {
      for (let j = i + 1; j < labs.length; j++) {
        minDistance = Math.min(minDistance, labDistance(labs[i], labs[j]))
      }
    }
    return { type, minDistance, atRisk: minDistance < SAFE_THRESHOLD }
  })
}