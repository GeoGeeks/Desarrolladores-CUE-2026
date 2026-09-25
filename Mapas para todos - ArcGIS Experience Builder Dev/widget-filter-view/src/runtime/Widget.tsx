import { React, type AllWidgetProps } from 'jimu-core'
import { JimuMapViewComponent, type JimuMapView } from 'jimu-arcgis'
import { Switch } from 'jimu-ui'
import Color from 'esri/Color'
import type Renderer from 'esri/renderers/Renderer'
import type SimpleRenderer from 'esri/renderers/SimpleRenderer'
import type UniqueValueRenderer from 'esri/renderers/UniqueValueRenderer'
import type ClassBreaksRenderer from 'esri/renderers/ClassBreaksRenderer'
import { analyzeColors, type CvdType, type CvdRisk } from './colorAccessibility'

const { useState, useRef, useEffect } = React

type DaltonismMode = 'none' | 'protanopia' | 'deuteranopia' | 'tritanopia'

const ACCESSIBLE_PALETTES: Record<Exclude<DaltonismMode, 'none'>, string[]> = {
  protanopia: ['#0072B2', '#E69F00', '#56B4E9', '#F0E442', '#CC79A7', '#D55E00', '#000000', '#FFFFFF'],
  deuteranopia: ['#0072B2', '#E69F00', '#56B4E9', '#F0E442', '#CC79A7', '#D55E00', '#000000', '#FFFFFF'],
  tritanopia: ['#D55E00', '#009E73', '#CC79A7', '#882255', '#44AA99', '#117733', '#000000', '#FFFFFF']
}

interface DetectedLayer {
  id: string
  title: string
  rendererType: string
  colors: string[]
}

function getColorHex(color: any): string {
  if (!color) return '#000000'
  return typeof color.toHex === 'function' ? color.toHex() : '#000000'
}

function getRendererColors(renderer: any): string[] {
  if (!renderer) return []
  if (renderer.type === 'simple') return [getColorHex(renderer.symbol?.color)]
  if (renderer.type === 'unique-value') return (renderer.uniqueValueInfos || []).map((i: any) => getColorHex(i.symbol?.color))
  if (renderer.type === 'class-breaks') return (renderer.classBreakInfos || []).map((i: any) => getColorHex(i.symbol?.color))
  return []
}

const Widget = (props: AllWidgetProps<any>) => {
  const [jimuMapView, setJimuMapView] = useState<JimuMapView>()
  const [mode, setMode] = useState<DaltonismMode>('none')
  const [useSvgFilter, setUseSvgFilter] = useState<boolean>(false)
  const [detectedLayers, setDetectedLayers] = useState<DetectedLayer[]>([])
  const originalRenderers = useRef(new WeakMap<any, Renderer>())

  const activeViewChangeHandler = (jmv: JimuMapView) => {
    if (jmv) setJimuMapView(jmv)
  }

  useEffect(() => {
    if (!jimuMapView) { setDetectedLayers([]); return }

    const detect = () => {
      const found: DetectedLayer[] = []
      jimuMapView.view.map.layers.forEach((layer: any) => {
        if (!layer.renderer) return
        found.push({
          id: layer.id,
          title: layer.title || layer.id,
          rendererType: layer.renderer.type,
          colors: getRendererColors(layer.renderer)
        })
      })
      setDetectedLayers(found)
    }

    detect()
    const handle = jimuMapView.view.map.layers.on('change', detect)
    return () => { handle?.remove() }
  }, [jimuMapView])

  const riskByType: Record<CvdType, CvdRisk> = React.useMemo(() => {
    const allColors = detectedLayers.flatMap(l => l.colors)
    const risks = analyzeColors(allColors)
    return {
      protanopia: risks.find(r => r.type === 'protanopia')!,
      deuteranopia: risks.find(r => r.type === 'deuteranopia')!,
      tritanopia: risks.find(r => r.type === 'tritanopia')!
    }
  }, [detectedLayers])

  const updateSymbolColor = (symbol: any, hexColor: string): any => {
    if (!symbol) return symbol
    const newSymbol = typeof symbol.clone === 'function' ? symbol.clone() : symbol
    
    const originalAlpha = symbol.color?.a ?? 0.6
    const newColor = new Color(hexColor)
    newColor.a = originalAlpha

    newSymbol.color = newColor
    return newSymbol
  }

  const applyPalette = (newMode: DaltonismMode) => {
    if (!jimuMapView) return

    if (useSvgFilter && jimuMapView.view?.container) {
      const container = jimuMapView.view.container
      container.style.filter = newMode === 'none' ? 'none' : `url(#${newMode}-filter)`
      setMode(newMode)
      return
    }

    jimuMapView.view.map.layers.forEach((layer: any) => {
      if (!layer.renderer) return

      if (!originalRenderers.current.has(layer)) {
        const origClone = typeof layer.renderer.clone === 'function' ? layer.renderer.clone() : layer.renderer
        originalRenderers.current.set(layer, origClone)
      }
      const original: any = originalRenderers.current.get(layer)
      if (!original) return

      if (newMode === 'none') {
        layer.renderer = typeof original.clone === 'function' ? original.clone() : original
        return
      }

      const palette = ACCESSIBLE_PALETTES[newMode]
      const renderer: any = typeof original.clone === 'function' ? original.clone() : original

      if (renderer.type === 'simple') {
        const r = renderer as SimpleRenderer
        r.symbol = updateSymbolColor(r.symbol, palette[0])
      } else if (renderer.type === 'unique-value') {
        const r = renderer as UniqueValueRenderer
        r.uniqueValueInfos = (r.uniqueValueInfos || []).map((info: any, i: number) => {
          const newInfo = { ...info }
          newInfo.symbol = updateSymbolColor(info.symbol, palette[i % palette.length])
          return newInfo
        })
      } else if (renderer.type === 'class-breaks') {
        const r = renderer as ClassBreaksRenderer
        r.classBreakInfos = (r.classBreakInfos || []).map((info: any, i: number) => {
          const newInfo = { ...info }
          newInfo.symbol = updateSymbolColor(info.symbol, palette[i % palette.length])
          return newInfo
        })
      }

      layer.renderer = renderer
    })

    setMode(newMode)
  }

  const hasMap = !!jimuMapView
  const labelFor: Record<CvdType, string> = {
    protanopia: 'Protanopia (Ceguera Rojo)',
    deuteranopia: 'Deuteranopia (Ceguera Verde)',
    tritanopia: 'Tritanopia (Ceguera Azul)'
  }

  return (
    <div className="widget-filter-view jimu-widget" style={{ overflow: 'auto', padding: '1rem' }}>
      <svg style={{ display: 'none' }}>
        <defs>
          <filter id="protanopia-filter">
            <feColorMatrix type="matrix" values="0.56667 0.43333 0 0 0  0.55833 0.44167 0 0 0  0 0.24167 0.75833 0 0  0 0 0 1 0" />
          </filter>
          <filter id="deuteranopia-filter">
            <feColorMatrix type="matrix" values="0.625 0.375 0 0 0  0.7 0.3 0 0 0  0 0.3 0.7 0 0  0 0 0 1 0" />
          </filter>
          <filter id="tritanopia-filter">
            <feColorMatrix type="matrix" values="0.95 0.05 0 0 0  0 0.43333 0.56667 0 0  0 0.475 0.525 0 0  0 0 0 1 0" />
          </filter>
        </defs>
      </svg>

      {props.useMapWidgetIds && props.useMapWidgetIds.length === 1 && (
        <JimuMapViewComponent useMapWidgetId={props.useMapWidgetIds?.[0]} onActiveViewChange={activeViewChangeHandler} />
      )}

      <p style={{ fontWeight: 'bold', marginBottom: '0.5rem' }}>Filtro Adaptativo de Daltonismo</p>

      {!hasMap && (
        <div style={{ padding: '0.75rem', marginBottom: '0.75rem', borderRadius: '4px', backgroundColor: '#FFF4E5', color: '#8A5300', fontSize: '0.85rem' }}>
          Selecciona un mapa en la configuración del widget para poder aplicar el filtro.
        </div>
      )}

      <div style={{ marginBottom: '1rem' }}>
        <label style={{ fontSize: '0.8rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <input
            type="checkbox"
            checked={useSvgFilter}
            onChange={(e) => {
              setUseSvgFilter(e.target.checked)
              applyPalette('none')
            }}
          />
          Usar corrección global por lente de matriz (SVG)
        </label>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', opacity: hasMap ? 1 : 0.5 }}>
        {(['protanopia', 'deuteranopia', 'tritanopia'] as CvdType[]).map(type => {
          const risk = riskByType[type]
          return (
            <div key={type} style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                <Switch
                  disabled={!hasMap}
                  checked={mode === type}
                  onChange={(_e: any, checked: boolean) => applyPalette(checked ? type : 'none')}
                />
                <span style={{ fontSize: '0.9rem' }}>{labelFor[type]}</span>
              </label>

              {hasMap && risk && (
                <span style={{ fontSize: '0.75rem', paddingLeft: '2.5rem', color: risk.atRisk ? '#B0433D' : '#1C7293' }}>
                  {risk.atRisk ? '⚠ Riesgo: Colores poco distinguibles' : '✅ Capas actualmente distinguibles'}
                </span>
              )}
            </div>
          )
        })}
      </div>
    </div>
  )
}

export default Widget