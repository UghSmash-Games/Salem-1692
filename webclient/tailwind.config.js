/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // Colonial Salem palette
        parchment: '#e8dcc0',
        ink: '#1c1410',
        ember: '#b5452f',
        moss: '#4a5d3a',
        candle: '#d9a441',
      },
    },
  },
  plugins: [],
};
