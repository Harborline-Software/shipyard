export interface LoadingStateProps {
  label: string
  variant?: 'page' | 'inline'
}

export function LoadingState({ label, variant = 'page' }: LoadingStateProps) {
  if (variant === 'inline') {
    return <p className="text-sm text-gray-500">{label}</p>
  }
  return (
    <div className="flex items-center justify-center h-48 text-gray-500">
      {label}
    </div>
  )
}
