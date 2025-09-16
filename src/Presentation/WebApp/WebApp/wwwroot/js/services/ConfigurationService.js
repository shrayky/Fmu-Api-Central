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