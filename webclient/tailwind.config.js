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

        // The HOST screen's locked Phase-7 palette (docs/phase-7-editor-steps.md), used by the
        // mirror so both TVs render the same design. Kept as a separate group deliberately: the
        // phone's `ember`/`ink` are different colours and renaming them would restyle every phone
        // screen.
        host: {
          ground: '#17130f',
          parchment: '#e8dcc0',
          bright: '#f0e6cd',
          ember: '#e6b268',   // ACTIVE TURN ring
          amber: '#c98a3f',
          crimson: '#a8231b', // xN badge, HANGED border
          hanged: '#e0463a',  // the HANGED word
          badge: '#f7efdd',
          asylum: '#2c4a7c',  // Asylum effect pill
          effect: '#7c2b23',  // every other effect pill
        },
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
