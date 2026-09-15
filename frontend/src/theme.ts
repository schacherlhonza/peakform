import { createTheme, type MantineColorsTuple } from '@mantine/core';

const brand: MantineColorsTuple = [
  '#eef6ff',
  '#dbe9fb',
  '#b3d1f5',
  '#88b8ef',
  '#66a3ea',
  '#5195e7',
  '#458de6',
  '#367acc',
  '#2c6cb7',
  '#1c5ba1',
];

export const theme = createTheme({
  primaryColor: 'brand',
  colors: { brand },
  fontFamily: 'Inter, system-ui, -apple-system, "Segoe UI", sans-serif',
  defaultRadius: 'md',
  headings: { fontWeight: '600' },
});
