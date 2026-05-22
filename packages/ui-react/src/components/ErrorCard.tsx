export interface ErrorCardProps {
  title: string
  message?: string
  onRetry?: () => void
  variant?: 'page' | 'default' | 'compact'
}

export function ErrorCard({ title, message, onRetry, variant = 'default' }: ErrorCardProps) {
  const padding = variant === 'page' ? 'p-8' : variant === 'default' ? 'p-6' : 'p-4'

  return (
    <div role="alert" className={`rounded-lg border border-red-200 bg-red-50 ${padding}`}>
      {variant === 'page' ? (
        <h2 className="text-xl font-bold text-red-700">{title}</h2>
      ) : (
        <p className="font-semibold text-red-700">{title}</p>
      )}
      {message && (
        <p className="mt-1 text-sm text-gray-600">{message}</p>
      )}
      {onRetry && (
        <button
          onClick={onRetry}
          className={
            variant === 'compact'
              ? 'mt-2 rounded bg-blue-600 px-3 py-1 text-xs text-white hover:bg-blue-700'
              : 'mt-3 rounded bg-blue-600 px-4 py-2 text-sm text-white hover:bg-blue-700'
          }
        >
          Retry
        </button>
      )}
    </div>
  )
}
