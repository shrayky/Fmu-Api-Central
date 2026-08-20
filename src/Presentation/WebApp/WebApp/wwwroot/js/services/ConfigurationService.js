// wwwroot/js/services/ConfigurationService.js

import { AuthService } from './AuthService.js';

export async function loadConfiguration() {
  return await AuthService.makeAuthenticatedRequest('/api/Configuration', {
    method: 'GET'
  });
}

export async function saveConfiguration(parameters) {
  return await AuthService.makeAuthenticatedRequest('/api/Configuration', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(parameters)
  });
}

export async function saveConfigurationSection(sectionKey, produceSection) {
  const loadResult = await loadConfiguration();
  
  if (!loadResult.result) 
    return loadResult;

  const parameters = loadResult.value.Content;
  const previousSection = parameters?.[sectionKey] ?? {};
  parameters[sectionKey] = produceSection(previousSection);
  
  const content = {
    Content: parameters
  };

  return await saveConfiguration(content);
}

export async function saveConfigurationSections(updaters) {
  const loadResult = await loadConfiguration();
  
  if (!loadResult.result) return
   loadResult;

  const parameters = loadResult.value.Content;

  for (const [sectionKey, producer] of Object.entries(updaters)) {
    const prev = parameters?.[sectionKey] ?? {};
    parameters[sectionKey] = typeof producer === 'function' ? producer(prev) : producer;
  }

  const content = {
    Content: parameters
  };

  return await saveConfiguration(content);
}

/**
 * Скачивает JSON переносимых настроек приложения.
 */
export async function exportPortableSettings() {
  const token = await AuthService.getValidToken();
  const response = await fetch(`${AuthService.getServerUrl()}/api/Configuration/export`, {
    method: 'GET',
    headers: {
      'Authorization': `Bearer ${token}`
    }
  });

  if (response.status === 401) {
    AuthService.redirectToLogin();
    return { result: false, error: 'Unauthorized', value: null };
  }

  if (!response.ok) {
    const errorText = await response.text();
    return { result: false, error: errorText, value: null };
  }

  const blob = await response.blob();
  return {
    result: true,
    error: null,
    value: {
      blob,
      fileName: fileNameFromDisposition(response.headers.get('Content-Disposition'), 'fmu-api-central-settings.json')
    }
  };
}

/**
 * Загружает JSON переносимых настроек приложения.
 */
export async function importPortableSettings(file) {
  const formData = new FormData();
  formData.append("file", file);

  return await AuthService.makeAuthenticatedRequest('/api/Configuration/import', {
    method: 'POST',
    body: formData
  });
}

function fileNameFromDisposition(disposition, fallback) {
  if (!disposition)
    return fallback;

  const utfMatch = disposition.match(/filename\*=UTF-8''([^;]+)/i);
  if (utfMatch)
    return decodeURIComponent(utfMatch[1]);

  const match = disposition.match(/filename="?([^"]+)"?/i);
  if (match)
    return match[1];

  return fallback;
}
