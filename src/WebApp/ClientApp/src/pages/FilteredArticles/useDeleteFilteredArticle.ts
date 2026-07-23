import { useMutation, useQueryClient } from '@tanstack/react-query';
import { deleteFilteredArticle } from '../../api/filteredArticles';

export function useDeleteFilteredArticle() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => deleteFilteredArticle(id),
    // A low-value diagnostic log, not a business record - a plain refetch of whichever page is
    // currently shown is proportionate here, unlike the News Feed page's more careful in-place
    // cache surgery for real article deletes.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['filteredArticles'] }),
  });
}
