export type StatusPillKind =
  | 'glAccountType'
  | 'occupancyStatus'
  | 'agingBucket'
  | 'balanceState'
  | 'workOrderStatus'

export interface StatusPillProps {
  kind: StatusPillKind
  value: string
  tooltip?: string
  outlined?: boolean
}

// -------------------------------------------------------------------------
// Color maps per kind — all from tokens.md / PAO Q6+Q7 direction
// -------------------------------------------------------------------------

const GL_ACCOUNT_TYPE: Record<string, string> = {
  Asset:     'bg-blue-100 text-blue-700',
  Liability: 'bg-purple-100 text-purple-700',
  Equity:    'bg-slate-100 text-slate-700',
  Revenue:   'bg-green-100 text-green-700',
  Expense:   'bg-amber-100 text-amber-800',
}

const OCCUPANCY_STATUS: Record<string, string> = {
  Occupied:    'bg-green-100 text-green-700',
  NoticeGiven: 'bg-amber-100 text-amber-800',
  Vacant:      'bg-gray-100 text-gray-700',
  OffMarket:   'bg-gray-100 border border-gray-300 text-gray-600',
}

const AGING_BUCKET: Record<string, string> = {
  NoBalance:  'bg-gray-50 text-gray-400',
  Current:    'bg-gray-100 text-gray-600',
  Days0To30:  'bg-yellow-100 text-yellow-700',
  Days31To60: 'bg-orange-100 text-orange-700',
  Days61To90: 'bg-orange-100 text-orange-800',
  Days90Plus: 'bg-red-100 text-red-700',
}

const BALANCE_STATE: Record<string, string> = {
  Balanced:     'bg-green-100 text-green-700',
  OutOfBalance: 'bg-red-100 text-red-700',
}

const WORK_ORDER_STATUS: Record<string, string> = {
  Draft:      'bg-blue-100 text-blue-700',
  Sent:       'bg-purple-100 text-purple-700',
  Accepted:   'bg-indigo-100 text-indigo-700',
  Scheduled:  'bg-yellow-100 text-yellow-700',
  InProgress: 'bg-orange-100 text-orange-700',
  Completed:  'bg-green-100 text-green-700',
  OnHold:     'bg-gray-100 text-gray-700',
  Cancelled:  'bg-red-100 text-red-700',
}

const FALLBACK = 'bg-gray-100 text-gray-700'

function resolveClasses(kind: StatusPillKind, value: string, outlined: boolean): string {
  const map: Record<StatusPillKind, Record<string, string>> = {
    glAccountType:   GL_ACCOUNT_TYPE,
    occupancyStatus: OCCUPANCY_STATUS,
    agingBucket:     AGING_BUCKET,
    balanceState:    BALANCE_STATE,
    workOrderStatus: WORK_ORDER_STATUS,
  }
  const base = map[kind][value] ?? FALLBACK
  // outlined override — adds border if not already present in the color map entry
  if (outlined && !base.includes('border')) return `${base} border border-current`
  return base
}

export function StatusPill({ kind, value, tooltip, outlined = false }: StatusPillProps) {
  const classes = resolveClasses(kind, value, outlined)
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${classes}`}
      title={tooltip}
    >
      {value}
    </span>
  )
}
