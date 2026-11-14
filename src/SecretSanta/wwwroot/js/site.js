document.addEventListener('submit', (e) => {
  const message = e.target.dataset.confirm;
  if (message && !window.confirm(message)) e.preventDefault();
});

// Shrinks the font until whole words fit the card; only a word too long even at 10px is broken.
function fitName(face, name, max = 15) {
  face.textContent = name;
  face.style.overflowWrap = '';
  face.style.fontSize = `${max}px`;
  const fits = () => face.scrollWidth <= face.clientWidth && face.scrollHeight <= face.clientHeight;
  for (let size = max; size > 10 && !fits(); size--) face.style.fontSize = `${size - 1}px`;
  if (!fits()) face.style.overflowWrap = 'anywhere';
}

const calm = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

// Throws gold stars, snowflakes and confetti out of the card; each piece flies once and the layer is removed.
function sparkle(card) {
  const box = card.getBoundingClientRect();
  const layer = document.createElement('div');
  layer.className = 'sparkles';
  layer.style.transform = `translate(${box.left + box.width / 2}px, ${box.top + box.height / 2}px)`;
  // U+FE0E keeps the snowflake a text glyph: some phones would draw it as an emoji.
  const shapes = ['✦', '❄︎', '★', '●', '✦', '❅'];
  const colors = ['#f6e2a8', '#e6b84a', '#ffffff', '#e0344f', '#47e08e', '#9fd3ff'];
  for (let i = 0; i < 26; i++) {
    const piece = document.createElement('span');
    piece.textContent = shapes[i % shapes.length];
    piece.style.color = colors[i % colors.length];
    piece.style.fontSize = `${9 + Math.random() * 11}px`;
    layer.append(piece);
    const angle = (Math.PI * 2 * i) / 26 + Math.random() * 0.4;
    const reach = 60 + Math.random() * 70;
    const x = Math.cos(angle) * reach;
    const y = Math.sin(angle) * reach;
    piece.animate([
      { transform: 'translate(-50%, -50%) scale(0.3) rotate(0deg)', opacity: 1 },
      { transform: `translate(calc(-50% + ${x * 0.8}px), calc(-50% + ${y * 0.8 - 10}px)) scale(1.1) rotate(${Math.random() * 180}deg)`, opacity: 1, offset: 0.55 },
      { transform: `translate(calc(-50% + ${x}px), calc(-50% + ${y + 28}px)) scale(0.7) rotate(${Math.random() * 360}deg)`, opacity: 0 },
    ], { duration: 1000 + Math.random() * 500, easing: 'cubic-bezier(0.2, 0.7, 0.3, 1)', fill: 'forwards' });
  }
  document.body.append(layer);
  setTimeout(() => layer.remove(), 1600);
}

// Card pick: shake, flip with sparkles, record the reveal in the background, then show the wishlist.
// Without JavaScript the card is a plain submit button and the server renders the revealed page.
document.addEventListener('click', (e) => {
  const card = e.target.closest('.playcard');
  if (!card) return;
  e.preventDefault();
  const deck = card.closest('.deck');
  if (deck.classList.contains('done')) return;
  const body = new FormData(deck);
  deck.classList.add('done');
  deck.querySelectorAll('.playcard').forEach((b) => { b.disabled = true; });
  fitName(card.querySelector('.face'), deck.dataset.name);
  fetch(deck.action, { method: 'POST', body, credentials: 'same-origin' }).catch(() => {});

  // After the show the other cards go away, so the opened card and the wishlist under it fit on one screen.
  const result = document.getElementById('reveal-result');
  const settle = () => {
    deck.classList.add('collapsed');
    fitName(card.querySelector('.face'), deck.dataset.name, 22);
    result.hidden = false;
    if (!calm) result.classList.add('appear');
    const top = card.getBoundingClientRect().top;
    if (top < 0 || result.getBoundingClientRect().top > window.innerHeight - 120) {
      window.scrollBy({ top: top - 90, behavior: calm ? 'auto' : 'smooth' });
    }
  };
  const flip = () => {
    card.classList.remove('opening');
    card.classList.add('flipped');
    if (calm) { settle(); return; }
    setTimeout(() => sparkle(card), 420);
    setTimeout(settle, 1700);
  };
  if (calm) { flip(); return; }
  card.classList.add('opening');
  setTimeout(flip, 320);
});

document.addEventListener('click', async (e) => {
  const button = e.target.closest('[data-copy]');
  if (!button) return;
  const input = button.parentElement.querySelector('input');
  try {
    await navigator.clipboard.writeText(input.value);
  } catch {
    input.select();
    document.execCommand('copy');
  }
  const label = button.textContent;
  button.textContent = button.dataset.copied;
  setTimeout(() => { button.textContent = label; }, 1500);
});
