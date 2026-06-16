import { useRef, useState } from 'react';
import type { ImportResult } from '../types';

type ImportBoxProps<T> = {
  accept: string;
  label: string;
  onUpload: (file: File) => Promise<T>;
  onDone?: (result: T) => void;
};

export function ImportBox<T>({ accept, label, onUpload, onDone }: ImportBoxProps<T>) {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [errors, setErrors] = useState<string[]>([]);

  async function submit() {
    if (!file) {
      setMessage('Sélectionnez un fichier.');
      return;
    }

    setLoading(true);
    setMessage('');
    setErrors([]);
    try {
      const result = await onUpload(file);
      onDone?.(result);
      setMessage(formatResult(result));
      setFile(null);
      if (inputRef.current) {
        inputRef.current.value = '';
      }
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Import impossible.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="panel import-box">
      <div className="import-row">
        <label>
          {label}
          <input ref={inputRef} type="file" accept={accept} onChange={(event) => setFile(event.target.files?.[0] ?? null)} />
        </label>
        <span className="selected-file">{file ? file.name : 'Aucun fichier choisi'}</span>
        <button type="button" onClick={submit} disabled={loading}>
          {loading ? 'Import...' : 'Importer'}
        </button>
      </div>
      {message && <p className="status">{message}</p>}
      {errors.length > 0 && (
        <ul className="error-list">
          {errors.map((error) => (
            <li key={error}>{error}</li>
          ))}
        </ul>
      )}
    </div>
  );

  function formatResult(result: T) {
    const importResult = result as ImportResult;
    if (typeof importResult.imported === 'number' && Array.isArray(importResult.errors)) {
      setErrors(importResult.errors.slice(0, 8).map((error) => `Ligne ${error.row} : ${error.message}`));
      return `${importResult.imported} importés, ${importResult.updated} mis à jour, ${importResult.skipped} ignorés.`;
    }

    return 'Import terminé.';
  }
}
