import { useMutation } from '@tanstack/react-query';
import { testApiEndpoint } from '../../api/providers';

export function useTestApiEndpoint() {
  return useMutation({
    mutationFn: ({ country, provider, endpointName }: { country: string; provider: string; endpointName: string }) =>
      testApiEndpoint(country, provider, endpointName),
  });
}
