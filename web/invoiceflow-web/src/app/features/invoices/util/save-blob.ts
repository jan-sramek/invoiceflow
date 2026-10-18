/** Hands a downloaded file to the browser's save flow under the given name. */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.rel = 'noopener';
  document.body.appendChild(link);
  link.click();
  link.remove();
  // Revoked later so the browser has started the download.
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
