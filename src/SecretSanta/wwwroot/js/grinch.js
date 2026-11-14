// A grumpy green mascot that wanders over the page like a desktop pet (in the spirit of Shimeji):
// it walks along the bottom of the window and on top of cards, climbs the sides of the window,
// leaps from card to card, sits on corners, and can be picked up with the mouse or a finger and thrown.
// Purely decorative: nothing depends on it, and it stays away when the visitor prefers reduced motion.
// Only SVG presentation attributes and CSSOM styles are used, so it also works under a strict CSP.
(() => {
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

  // Drawn facing right; the feet stand at (32, 78.5). Parts with data-part are moved every frame.
  const SPRITE = `
<svg viewBox="8 -2 48 81" focusable="false">
  <g data-part="flip">
    <g data-part="rig" class="rig">
      <g data-part="legB">
        <path d="M28.5 59V75.5" stroke="#3c6d1e" stroke-width="4.6" stroke-linecap="round"/>
        <path d="M28.5 59V75.5" stroke="#5c9530" stroke-width="2.8" stroke-linecap="round"/>
        <ellipse cx="30.3" cy="76.6" rx="3.6" ry="1.9" fill="#3c6d1e"/>
      </g>
      <g data-part="legF">
        <path d="M35.5 59V75.5" stroke="#3c6d1e" stroke-width="4.6" stroke-linecap="round"/>
        <path d="M35.5 59V75.5" stroke="#6aa838" stroke-width="2.8" stroke-linecap="round"/>
        <ellipse cx="37.3" cy="76.6" rx="3.6" ry="1.9" fill="#3c6d1e"/>
      </g>
      <g data-part="armB">
        <path d="M24.5 42L23 54" stroke="#3c6d1e" stroke-width="4.4" stroke-linecap="round"/>
        <path d="M24.5 42L23 54" stroke="#5c9530" stroke-width="2.6" stroke-linecap="round"/>
        <circle cx="23" cy="54.6" r="2.5" fill="#5c9530" stroke="#3c6d1e" stroke-width="1"/>
      </g>
      <path d="M42.8 51.9Q45.4 55.6 41.1 56.2Q42.0 60.5 38.1 59.5Q37.8 64.9 34.1 61.3Q32.0 64.2 29.8 61.3Q26.2 64.7 25.9 59.5Q22.1 60.2 22.8 56.1Q19.4 55.2 21.2 51.8Q18.4 49.4 21.2 47.1Q19.1 43.6 22.9 42.8Q21.4 37.9 25.9 39.5Q26.8 35.6 29.9 37.7Q32.1 33.7 34.2 37.7Q37.6 34.8 38.1 39.5Q42.0 38.7 41.2 42.9Q45.4 43.5 42.8 47.2Q45.5 49.6 42.8 51.9Z" fill="#79b843" stroke="#3c6d1e" stroke-width="1.1" stroke-linejoin="round"/>
      <ellipse cx="34" cy="51.5" rx="6" ry="7.5" fill="#b4dc7a"/>
      <path d="M25.2 39.6L22.4 47.4L25.9 48L28.2 40.6Z" fill="#b8253a" stroke="#8e1328" stroke-width="0.8" stroke-linejoin="round"/>
      <rect x="22.5" y="36.4" width="19" height="4.8" rx="2.4" fill="#d23347" stroke="#8e1328" stroke-width="0.8"/>
      <path d="M21.5 21.5L14.5 16L19 26ZM42.5 21.5L49.5 16L45 26Z" fill="#79b843" stroke="#3c6d1e" stroke-width="1.1" stroke-linejoin="round"/>
      <path d="M44.5 27.3Q47.5 30.4 43.4 31.5Q44.8 35.2 40.8 35.0Q41.5 39.9 37.2 37.5Q35.8 41.1 32.9 38.6Q30.3 42.5 28.6 38.1Q25.0 40.6 24.6 36.2Q20.9 36.8 21.6 33.1Q16.9 32.8 19.8 29.1Q16.6 27.2 19.5 24.7Q16.2 21.6 20.6 20.5Q19.4 16.9 23.2 17.0Q23.2 13.1 26.8 14.5Q28.0 10.2 31.1 13.4Q33.7 8.8 35.4 13.9Q38.8 11.9 39.4 15.8Q43.4 14.9 42.4 18.9Q47.3 19.1 44.2 22.9Q49.5 24.7 44.5 27.3Z" fill="#79b843" stroke="#3c6d1e" stroke-width="1.1" stroke-linejoin="round"/>
      <g data-part="eyes">
        <ellipse cx="30.4" cy="25.6" rx="3.1" ry="3.5" fill="#fffbe6"/>
        <ellipse cx="38" cy="25.6" rx="3.1" ry="3.5" fill="#fffbe6"/>
        <circle cx="31.4" cy="26.6" r="1.5" fill="#2b1d0e"/>
        <circle cx="39" cy="26.6" r="1.5" fill="#2b1d0e"/>
        <path d="M27 21.6H33.8V25Q30.4 26.2 27 25ZM34.6 21.6H41.4V25Q38 26.2 34.6 25Z" fill="#79b843"/>
        <path d="M27 25Q30.4 26.2 33.8 25M34.6 25Q38 26.2 41.4 25" fill="none" stroke="#3c6d1e" stroke-width="0.9" stroke-linecap="round"/>
      </g>
      <path d="M26.6 21.2L33 23.4M41.8 21.2L35.4 23.4" stroke="#2c5214" stroke-width="1.8" stroke-linecap="round"/>
      <ellipse cx="35.6" cy="29.8" rx="1.7" ry="1.1" fill="#3c6d1e"/>
      <path d="M29.4 34.2Q33.6 31.2 38.6 33.4" fill="none" stroke="#2c5214" stroke-width="1.3" stroke-linecap="round"/>
      <g transform="rotate(-8 32 13.5)">
        <path d="M20 13C20.5 4.5 26 0.5 32 0.5C38 0.5 43.5 4.5 44 13Z" fill="#d23347" stroke="#8e1328" stroke-width="1" stroke-linejoin="round"/>
        <path d="M31 1.2C25 -0.8 17.5 1 13.8 7.4L17 9C19.8 4.6 25.5 3.4 31.5 5.8Z" fill="#d23347" stroke="#8e1328" stroke-width="1" stroke-linejoin="round"/>
        <circle cx="14.6" cy="8.6" r="3.2" fill="#fff8ea" stroke="#dccdb0" stroke-width="0.8"/>
        <rect x="18.5" y="10.6" width="27" height="5.2" rx="2.6" fill="#fff8ea" stroke="#dccdb0" stroke-width="0.8"/>
      </g>
      <g data-part="armF">
        <path d="M39.5 42L41 54" stroke="#3c6d1e" stroke-width="4.4" stroke-linecap="round"/>
        <path d="M39.5 42L41 54" stroke="#6aa838" stroke-width="2.6" stroke-linecap="round"/>
        <circle cx="41" cy="54.6" r="2.5" fill="#6aa838" stroke="#3c6d1e" stroke-width="1"/>
      </g>
    </g>
  </g>
</svg>`;
  const VIEW_W = 48;
  const VIEW_H = 81;
  const GRAVITY = 1800; // px/s²
  const PLATFORMS = 'main .card, main .alert, main .letter, main .note';

  const holder = document.createElement('div');
  holder.className = 'grinch';
  holder.setAttribute('aria-hidden', 'true');
  holder.innerHTML = SPRITE;
  document.body.append(holder);

  const part = (name) => holder.querySelector(`[data-part="${name}"]`);
  const flip = part('flip');
  const rig = part('rig');
  const eyes = part('eyes');
  const legB = part('legB');
  const legF = part('legF');
  const armB = part('armB');
  const armF = part('armF');

  const rand = (a, b) => a + Math.random() * (b - a);
  const clamp = (v, a, b) => Math.min(b, Math.max(a, v));
  const pick = (list) => list[Math.floor(Math.random() * list.length)];
  const right = () => document.documentElement.clientWidth;
  const floor = () => window.innerHeight;

  let h = 0;
  let w = 0;
  let pace = 1;
  function size() {
    h = window.innerWidth < 600 ? 58 : 76;
    w = (h * VIEW_W) / VIEW_H;
    pace = h / 76;
    holder.style.width = `${w}px`;
    holder.style.height = `${h}px`;
  }
  size();
  window.addEventListener('resize', size);

  // What it can stand on: the top edges of cards that are on screen.
  let clock = 0;
  let candidates = [];
  let candidatesAt = -Infinity;
  function platforms() {
    if (clock - candidatesAt > 1000) {
      candidates = [...document.querySelectorAll(PLATFORMS)];
      candidatesAt = clock;
    }
    const list = [];
    for (const el of candidates) {
      const r = el.getBoundingClientRect();
      if (r.width >= w * 2 && r.top >= h * 0.7 && r.top <= floor() - 8) {
        list.push({ el, left: r.left, right: r.right, top: r.top });
      }
    }
    return list;
  }

  // x, y: the point between its feet, in viewport pixels.
  let x = right() * rand(0.25, 0.75);
  let y = -h;
  let vx = 0;
  let vy = 0;
  let dir = Math.random() < 0.5 ? -1 : 1;
  let state = 'fall';
  let timer = 0;
  let since = 0;
  let phase = 0;
  let on = null; // the card under its feet, { el, left }; null means the bottom of the window
  let wall = 0; // -1 or 1 while climbing the left or right side of the window
  let climbDir = -1;
  let blinkIn = 2;
  let blink = 0;
  let drag = null;

  function setState(next, time = 0) {
    state = next;
    timer = time;
    since = 0;
  }

  function jump(jx, jy) {
    on = null;
    vx = jx;
    vy = jy;
    setState('fall');
  }

  // A ballistic arc that comes down on the top edge of a card.
  function leap(p) {
    const tx = rand(p.left + w, p.right - w);
    const t = clamp(Math.hypot(tx - x, p.top - y) / 650, 0.45, 1.1) * rand(0.9, 1.1);
    jump((tx - x) / t, (p.top - y - 0.5 * GRAVITY * t * t) / t);
  }

  function land(p) {
    on = p ? { el: p.el, left: p.left } : null;
    y = p ? p.top : floor();
    vx = 0;
    vy = 0;
    setState('land', 0.25);
  }

  function reachable() {
    return platforms().filter((p) => (!on || p.el !== on.el) && Math.abs(p.top - y) < 380);
  }

  // Picks what to do next while standing.
  function next() {
    const r = Math.random();
    const targets = reachable();
    if (r < 0.12 && targets.length) return leap(pick(targets));
    if (r < 0.55) {
      if (Math.random() < 0.35) dir = -dir;
      return setState('walk', rand(2, 6));
    }
    if (r < 0.75) {
      if (Math.random() < 0.3) dir = -dir;
      return setState('idle', rand(1, 3));
    }
    if (r < 0.88) return setState(on ? 'idle' : 'sit', rand(3, 7));
    if (!on) {
      dir = x < right() / 2 ? -1 : 1; // off to the nearer side of the window, to climb it
      return setState('walk', 20);
    }
    return setState('walk', rand(3, 8));
  }

  function atEdge(lo, hi) {
    const r = Math.random();
    if (r < 0.3) {
      dir = -dir;
      return;
    }
    if (r < 0.55) {
      x = dir > 0 ? hi + w * 0.35 : lo - w * 0.35; // sit on the corner with the legs over the edge
      setState('dangle', rand(3, 6));
      return;
    }
    const targets = reachable();
    if (r < 0.8 && targets.length) leap(pick(targets));
    else jump(dir * rand(90, 170), -rand(150, 260));
  }

  function climb(side) {
    wall = side;
    dir = side;
    on = null;
    climbDir = -1;
    setState('climb', rand(1.5, 4));
  }

  function stand(dt) {
    let lo = w * 0.5;
    let hi = right() - w * 0.5;
    if (on) {
      const r = on.el.getBoundingClientRect();
      if (!on.el.isConnected || r.width === 0) return jump(0, 0);
      x += r.left - on.left; // ride along when the page scrolls or the layout moves
      on.left = r.left;
      y = r.top;
      if (y < h * 0.7 || x < r.left - w * 0.6 || x > r.right + w * 0.6) return jump(0, 0);
      if (y > floor()) on = null;
      else {
        lo = r.left + w * 0.25;
        hi = r.right - w * 0.25;
      }
    }
    if (!on) y = floor();

    timer -= dt;
    if (state === 'walk') {
      x += dir * 38 * pace * dt;
      phase += dt * 9;
      if (x <= lo || x >= hi) {
        x = clamp(x, lo, hi);
        if (on) atEdge(lo, hi);
        else if (Math.random() < 0.6) climb(x <= lo ? -1 : 1);
        else dir = -dir;
      }
    } else if (state !== 'dangle') x = clamp(x, lo, hi);

    if (timer <= 0 && state !== 'fall' && state !== 'climb') {
      x = clamp(x, lo, hi);
      next();
    }
  }

  function climbStep(dt) {
    x = wall < 0 ? w * 0.42 : right() - w * 0.42;
    y += climbDir * 30 * pace * dt;
    phase += dt * 7;
    timer -= dt;
    if (y >= floor()) {
      y = floor();
      dir = -wall;
      setState('walk', rand(1, 3));
      return;
    }
    if (y - h < 4) {
      y = h + 4;
      climbDir = 1;
    }
    if (timer > 0) return;
    const r = Math.random();
    const targets = platforms();
    if (r < 0.5 && targets.length) leap(pick(targets));
    else if (r < 0.65) jump(-wall * rand(150, 260), -rand(60, 160));
    else {
      climbDir = r < 0.8 ? 1 : -1;
      timer = rand(1, 3);
    }
  }

  function fallStep(dt) {
    const before = y;
    vy = Math.min(vy + GRAVITY * dt, 1600);
    x += vx * dt;
    y += vy * dt;
    phase += dt * 6;
    const lo = w * 0.5;
    const hi = right() - w * 0.5;
    if (x < lo) {
      x = lo;
      vx = Math.abs(vx) * 0.4;
    }
    if (x > hi) {
      x = hi;
      vx = -Math.abs(vx) * 0.4;
    }
    if (Math.abs(vx) > 20) dir = vx > 0 ? 1 : -1;
    if (vy <= 0) return;
    for (const p of platforms()) {
      if (before <= p.top && y >= p.top && x > p.left + w * 0.2 && x < p.right - w * 0.2) {
        land(p);
        return;
      }
    }
    if (y >= floor()) land(null);
  }

  holder.addEventListener('pointerdown', (e) => {
    if (e.button > 0 || drag) return;
    e.preventDefault();
    e.target.setPointerCapture(e.pointerId);
    const now = performance.now();
    drag = { id: e.pointerId, x0: e.clientX, y0: e.clientY, px: e.clientX, py: e.clientY, at: now, vx: 0, vy: 0, held: false };
  });

  holder.addEventListener('pointermove', (e) => {
    if (!drag || e.pointerId !== drag.id) return;
    const now = performance.now();
    const dt = Math.max(8, now - drag.at) / 1000;
    drag.vx = drag.vx * 0.5 + ((e.clientX - drag.px) / dt) * 0.5;
    drag.vy = drag.vy * 0.5 + ((e.clientY - drag.py) / dt) * 0.5;
    drag.px = e.clientX;
    drag.py = e.clientY;
    drag.at = now;
    if (!drag.held && Math.hypot(e.clientX - drag.x0, e.clientY - drag.y0) > 5) {
      drag.held = true;
      on = null;
      setState('drag');
      holder.classList.add('held');
    }
    if (drag.held) {
      x = e.clientX;
      y = e.clientY + h * 0.83; // held by the scruff of the neck
    }
  });

  function release(e) {
    if (!drag || e.pointerId !== drag.id) return;
    const d = drag;
    drag = null;
    holder.classList.remove('held');
    if (d.held) {
      const moving = performance.now() - d.at < 100; // a pointer that stopped before letting go just drops it
      jump(moving ? clamp(d.vx, -1500, 1500) : 0, moving ? clamp(d.vy, -1500, 1500) : 0);
    } else if (state === 'climb') jump(-wall * 120, 0);
    else if (state !== 'fall') jump(0, -rand(280, 360)); // poked: hops in annoyance
  }
  holder.addEventListener('pointerup', release);
  holder.addEventListener('pointercancel', release);
  // A finger on the grinch picks him up instead of scrolling the page.
  holder.addEventListener('touchstart', (e) => e.preventDefault(), { passive: false });

  function draw(dt) {
    blinkIn -= dt;
    if (blinkIn <= 0) {
      blink = 0.14;
      blinkIn = rand(2.5, 6);
    }
    blink = Math.max(0, blink - dt);
    eyes.setAttribute('transform', blink > 0 ? 'translate(0 25.6) scale(1 0.15) translate(0 -25.6)' : '');

    const s = Math.sin(phase);
    let legs = [0, 0];
    let arms = [0, 0];
    let body = '';
    switch (state) {
      case 'walk':
        legs = [24 * s, -24 * s];
        arms = [-18 * s, 18 * s];
        body = `translate(0 ${(-Math.abs(s) * 1.3).toFixed(2)})`;
        break;
      case 'land':
        body = 'translate(32 78.5) scale(1.12 0.86) translate(-32 -78.5)';
        break;
      case 'sit':
        legs = [-74, -84];
        arms = [-28, -38];
        body = 'translate(0 16.5)';
        break;
      case 'dangle': {
        const k = Math.sin(since * 3);
        legs = [12 * k, -12 * k];
        body = 'translate(0 17)';
        break;
      }
      case 'climb':
        legs = [-20 + 18 * s, -20 - 18 * s];
        arms = [-70 - 25 * s, -105 + 25 * s];
        break;
      case 'fall':
        legs = [16, -16];
        arms = [135 + 10 * s, -135 - 10 * s];
        break;
      case 'drag': {
        const tilt = clamp((drag ? drag.vx : 0) * 0.02, -35, 35) * dir;
        legs = [tilt * 0.5 + 10 * s, tilt * 0.5 - 10 * s];
        arms = [100 + 12 * s, -100 - 12 * s];
        body = `rotate(${tilt.toFixed(1)} 32 12)`;
        break;
      }
      default: // idle
        body = `translate(0 ${(Math.sin(since * 2.4) * 0.5).toFixed(2)})`;
    }
    legB.setAttribute('transform', `rotate(${legs[0].toFixed(1)} 28.5 59)`);
    legF.setAttribute('transform', `rotate(${legs[1].toFixed(1)} 35.5 59)`);
    armB.setAttribute('transform', `rotate(${arms[0].toFixed(1)} 24.5 42)`);
    armF.setAttribute('transform', `rotate(${arms[1].toFixed(1)} 39.5 42)`);
    rig.setAttribute('transform', body);
    flip.setAttribute('transform', dir < 0 ? 'matrix(-1 0 0 1 64 0)' : '');
    holder.style.transform = `translate3d(${(x - w / 2).toFixed(1)}px, ${(y - h).toFixed(1)}px, 0)`;
  }

  let last = performance.now();
  function frame(now) {
    const dt = Math.min(0.05, (now - last) / 1000);
    last = now;
    clock = now;
    since += dt;
    if (state === 'fall') fallStep(dt);
    else if (state === 'climb') climbStep(dt);
    else if (state === 'drag') {
      phase += dt * 8;
      if (drag) drag.vx *= Math.pow(0.05, dt); // let the swing settle while held still
      else jump(0, 0);
    } else stand(dt);
    draw(dt);
    requestAnimationFrame(frame);
  }
  requestAnimationFrame(frame);
})();
