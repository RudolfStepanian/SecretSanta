// Mail relay for the Secret Santa site: an Apps Script web app in the Gmail account made for the site.
// The site posts {"key", "to", "subject", "body"} and the script sends a plain-text e-mail from that account.
// Setup is described in the README (E-mail). The key lives in the script property KEY, never in this file.
function doPost(e) {
  const key = PropertiesService.getScriptProperties().getProperty('KEY');
  let data;
  try {
    data = JSON.parse(e.postData.contents);
  } catch (err) {
    return reply({ ok: false, error: 'not JSON' });
  }
  if (!key || data.key !== key) return reply({ ok: false, error: 'wrong key' });
  // One plain address only, so a leaked key can't turn the script into a mailer for lists.
  if (typeof data.to !== 'string' || !/^[^\s@,;<>"]+@[^\s@,;<>"]+\.[^\s@,;<>"]+$/.test(data.to)) {
    return reply({ ok: false, error: 'not one address' });
  }
  try {
    MailApp.sendEmail({ to: data.to, subject: String(data.subject), body: String(data.body), name: 'Secret Santa' });
    return reply({ ok: true, left: MailApp.getRemainingDailyQuota() });
  } catch (err) {
    return reply({ ok: false, error: String(err) });
  }
}

function reply(result) {
  return ContentService.createTextOutput(JSON.stringify(result)).setMimeType(ContentService.MimeType.JSON);
}
