document.addEventListener('submit', (e) => {
  const message = e.target.dataset.confirm;
  if (message && !window.confirm(message)) e.preventDefault();
});

// Shrinks the font until whole words fit the card; only a word too long even at 10px is broken.
function fitName(face, name) {
  face.textContent = name;
  const fits = () => face.scrollWidth <= face.clientWidth && face.scrollHeight <= face.clientHeight;
  for (let size = 15; size > 10 && !fits(); size--) face.style.fontSize = `${size - 1}px`;
  if (!fits()) face.style.overflowWrap = 'anywhere';
}

// Card pick: flip the clicked card at once, record the reveal in the background, then show the wishlist.
// Without JavaScript the card is a plain submit button and the server renders the revealed page.
document.addEventListener('click', (e) => {
  const card = e.target.closest('.playcard');
  if (!card) return;
  e.preventDefault();
  const deck = card.closest('.deck');
  if (deck.classList.contains('done')) return;
  const body = new FormData(deck);
  deck.classList.add('done');
  fitName(card.querySelector('.face'), deck.dataset.name);
  card.classList.add('flipped');
  deck.querySelectorAll('.playcard').forEach((b) => { b.disabled = true; });
  fetch(deck.action, { method: 'POST', body, credentials: 'same-origin' }).catch(() => {});
  setTimeout(() => { document.getElementById('reveal-result').hidden = false; }, 650);
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
