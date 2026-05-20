import type { Preview } from '@storybook/react'
import './preview.css'

const preview: Preview = {
  parameters: {
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
    backgrounds: {
      default: 'light',
      values: [
        { name: 'light', value: '#ffffff' },
        { name: 'muted', value: '#f3f4f6' },
      ],
    },
  },
}

export default preview
