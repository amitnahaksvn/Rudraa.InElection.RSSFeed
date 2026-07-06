import { useQuery } from '@tanstack/react-query';
import { fetchApiProviders } from '../../api/providers';

export function useApiProviders() {
  return useQuery({ queryKey: ['apiProviders'], queryFn: fetchApiProviders });
}
