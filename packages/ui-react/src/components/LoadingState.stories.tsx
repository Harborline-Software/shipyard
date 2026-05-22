import type { Meta, StoryObj } from '@storybook/react'
import { LoadingState } from './LoadingState'

const meta: Meta<typeof LoadingState> = {
  title: 'UI/LoadingState',
  component: LoadingState,
  tags: ['autodocs'],
  parameters: {
    docs: {
      description: {
        component:
          'Consistent loading placeholder used while async data is in-flight. ' +
          '`page` variant (default) renders a centered h-48 div — used for full page/section loaders. ' +
          '`inline` variant renders a text-sm paragraph — used for sub-section or inline loaders.',
      },
    },
    layout: 'padded',
  },
}

export default meta
type Story = StoryObj<typeof LoadingState>

export const Page: Story = {
  args: {
    label: 'Loading properties…',
    variant: 'page',
  },
  parameters: {
    docs: {
      description: {
        story: 'Full-section loader — centered in a 192px (h-48) container.',
      },
    },
  },
}

export const Inline: Story = {
  args: {
    label: 'Loading payments…',
    variant: 'inline',
  },
  parameters: {
    docs: {
      description: {
        story: 'Inline loader — text-sm paragraph, used inside table/list sections.',
      },
    },
  },
}
