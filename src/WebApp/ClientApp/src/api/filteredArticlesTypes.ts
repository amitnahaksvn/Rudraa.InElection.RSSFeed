// Mirrors Application/FilteredArticles/Dtos/FilteredArticleDto.cs.
import type { ArticleSourceType } from './newsTypes';

export interface FilteredArticle {
  id: string;
  provider: string;
  title: string;
  summary: string | null;
  category: string;
  sourceType: ArticleSourceType;
  pulledAt: string;
}
