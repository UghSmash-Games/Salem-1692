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
      keyframes: {
        fadeIn: {
          from: { opacity: '0', transform: 'scale(0.92)' },
          to: { opacity: '1', transform: 'scale(1)' },
        },
      },
      animation: {
        fadeIn: 'fadeIn 300ms ease-out',
      },
    },
  },
  plugins: [],
};
