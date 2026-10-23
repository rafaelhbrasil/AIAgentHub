/**
 * Strips trailing path separators (/, \) in linear time.
 * Replaces an equivalent `replace(/[/\\]+$/, '')` regex, which backtracks
 * super-linearly on long separator runs (Sonar S8786).
 */
export function stripTrailingSeparators(value: string): string {
  let end = value.length;
  while (end > 0 && (value[end - 1] === '/' || value[end - 1] === '\\')) {
    end--;
  }
  return value.slice(0, end);
}

export function normalizePath(path: string): string {
  const clean = stripTrailingSeparators(path.trim().replace(/\\/g, '/'));
  return clean === '' ? '/' : clean;
}

export function isPathForbiddenForBrowsing(
  rawPath: string | null | undefined,
  forbiddenPatterns: string[]
): boolean {
  if (!rawPath || !rawPath.trim()) return false;

  const normalized = normalizePath(rawPath);

  for (const pattern of forbiddenPatterns) {
    if (!pattern) continue;
    const normPattern = normalizePath(pattern);

    // If pattern contains wildcard "*:" or "*"
    if (normPattern.includes('*')) {
      const regexPattern =
        '^' +
        normPattern
          .replace(/[.*+?^${}()|[\]\\]/g, String.raw`\$&`)
          .replace(/\\\*/g, '[^/]+') +
        '(/.*)?$';
      const re = new RegExp(regexPattern, 'i');
      if (re.test(normalized)) {
        return true;
      }
    } else {
      // Direct prefix or exact match
      const lowerPath = normalized.toLowerCase();
      const lowerPattern = normPattern.toLowerCase();
      if (lowerPath === lowerPattern || lowerPath.startsWith(lowerPattern + '/')) {
        return true;
      }
    }
  }

  return false;
}

export function isPathForbiddenForWorkspace(
  rawPath: string | null | undefined,
  forbiddenPatterns: string[]
): boolean {
  if (!rawPath || !rawPath.trim()) return true;

  const normalized = normalizePath(rawPath);

  // 1. Check if it matches any protected system folder pattern
  if (isPathForbiddenForBrowsing(rawPath, forbiddenPatterns)) {
    return true;
  }

  // 2. Check if bare root drive (e.g. "C:", "C:/", "D:\", "/")
  if (/^[a-zA-Z]:\/?$/.test(normalized) || normalized === '/') {
    return true;
  }

  return false;
}

export const isPathForbidden = isPathForbiddenForWorkspace;
