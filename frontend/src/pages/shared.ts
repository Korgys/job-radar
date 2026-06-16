export function formatList(values: string[] | undefined | null) {
  return values && values.length > 0 ? values.join(', ') : 'Non renseigné';
}

export function matchesText(search: string, ...values: string[]) {
  if (!search.trim()) {
    return true;
  }

  const normalizedSearch = normalize(search);
  return values.some((value) => normalize(value).includes(normalizedSearch));
}

export function normalize(value: string | undefined | null) {
  return (value ?? '')
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLowerCase();
}

export function domainColor(domain: string) {
  const key = normalize(domain);
  const colors: Record<string, string> = {
    sante: '#d84d8d',
    industrie: '#6e7681',
    banque: '#d6a700',
    assurance: '#e0792f',
    esn: '#2b73c8',
    saas: '#7b4cc2',
    'service public': '#2f9d63',
    energie: '#00a5a5',
    retail: '#c83232',
    autre: '#222831'
  };

  return colors[key] ?? '#222831';
}

export function unique(values: Array<string | null | undefined>) {
  return Array.from(new Set(values.filter((value): value is string => Boolean(value && value.trim())))).sort((a, b) => a.localeCompare(b));
}
