import { test, expect } from '@playwright/test';

function watchErrors(page) {
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => { if (m.type() === 'error') errors.push(m.text()); });
  return errors;
}

async function startSeries(page, team = 'The Test Pressing') {
  await page.goto('./');
  await expect(page.locator('.title-screen')).toBeVisible({ timeout: 30_000 });
  await page.getByRole('button', { name: /start the first series|start a new series/i }).click();
  await page.locator('input').fill(team);
  await page.getByRole('button', { name: 'Print issue #1' }).click();
  await expect(page.locator('.issue .cover')).toBeVisible();
}

async function pickTeam(page, names) {
  await page.getByRole('button', { name: /assemble the team/i }).click();
  const cards = page.locator('.hero-card');
  for (let i = 0; i < await cards.count(); i++) {
    if (await cards.nth(i).evaluate((e) => e.classList.contains('on'))) await cards.nth(i).click();
  }
  for (const n of names) await page.locator('.hero-card', { hasText: n }).click();
  await expect(page.locator('.hero-card.on')).toHaveCount(names.length);
  await page.getByRole('button', { name: /fight!/i }).click();
  await expect(page.locator('.board')).toBeVisible();
}

async function fightToEnd(page) {
  for (let i = 0; i < 400; i++) {
    if (await page.locator('.end-stamp').isVisible()) return;
    const auto = page.getByRole('button', { name: 'Auto' });
    if (await auto.isEnabled()) await auto.click();
    else await page.locator('.board').click({ position: { x: 5, y: 5 } }); // skip the replay
    await page.waitForTimeout(100);
  }
  throw new Error('the fight never ended');
}

async function clickTile(page, locator) {
  const box = await locator.boundingBox();
  await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
}

test('the title page loads and draws its rogues', async ({ page }) => {
  const errors = watchErrors(page);
  await page.goto('./');
  await expect(page.locator('h1.masthead')).toHaveText('NEMESIS', { timeout: 30_000 });
  await expect(page.locator('.rogue')).toHaveCount(5);
  await expect(page.getByRole('button', { name: 'Start the first series' })).toBeVisible();
  expect(errors).toEqual([]);
});

test('a hero can move and use a power', async ({ page }) => {
  const errors = watchErrors(page);
  await startSeries(page);
  await pickTeam(page, ['Bulwark', 'Noonday', 'Patchwork']);

  await expect(page.locator('.panel.powers')).toBeVisible();
  await page.locator('.team-card', { hasText: 'Noonday' }).click();
  await expect(page.locator('.panel.powers h3')).toContainText('Noonday');
  await expect(page.locator('.hl-move').first()).toBeVisible();
  await clickTile(page, page.locator('.hl-move').first());
  await expect(page.locator('.hl-move')).toHaveCount(0);

  await page.getByRole('button', { name: /^Nova / }).click();
  await expect(page.locator('.hl-target').first()).toBeVisible();
  await clickTile(page, page.locator('.hl-target').first());
  await expect(page.locator('.cap', { hasText: 'Noonday: Nova!' })).toBeVisible();
  // Noonday has moved and acted, so the next hero is up
  await expect(page.locator('.team-card.on')).not.toContainText('Noonday');
  expect(errors).toEqual([]);
});

test('an issue plays through, files its cover and is remembered after a reload', async ({ page }) => {
  const errors = watchErrors(page);
  await startSeries(page, 'The Night Shift');
  const villain = (await page.locator('.issue .vfile h3').first().innerText()).split('\n')[0].trim();
  await pickTeam(page, ['Bulwark', 'Kestrel', 'Vesper']);
  await fightToEnd(page);
  await expect(page.locator('.stamp-word')).toHaveText(/CAPTURED!|GOT AWAY!|DEFEATED/);
  await page.getByRole('button', { name: /read the aftermath/i }).click();

  await expect(page.locator('.aftermath .cover')).toBeVisible();
  await expect(page.locator('.note').first()).toBeVisible();

  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: /save cover as png/i }).click();
  const file = await download;
  expect(file.suggestedFilename()).toMatch(/^nemesis-s1-01-.*\.png$/);

  if (await page.locator('.offer').count()) await page.locator('.offer').first().click();
  await page.getByRole('button', { name: /next issue|how did the series end/i }).click();
  await expect(page.locator('.status')).toContainText('issue 2/6');

  await page.reload();
  await expect(page.getByRole('button', { name: /continue: the night shift, issue #2/i })).toBeVisible({ timeout: 30_000 });
  await page.getByRole('button', { name: /rogues gallery/i }).click();
  await expect(page.locator('.vfile h3', { hasText: villain })).toBeVisible();
  await expect(page.locator('.vf-history li').first()).toContainText('First appeared');
  expect(errors).toEqual([]);
});

test('fits a phone screen', async ({ page }) => {
  const errors = watchErrors(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await startSeries(page);
  await pickTeam(page, ['Bulwark', 'Kestrel', 'Reverie']);
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(overflow).toBeLessThanOrEqual(0);
  await expect(page.locator('.board-svg')).toBeVisible();
  expect(errors).toEqual([]);
});
