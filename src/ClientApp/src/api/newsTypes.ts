// Mirrors Application/News/Dtos/NewsArticleDto.cs - keep in sync by hand (no shared schema
// generation in this project yet).

export type ArticleSourceType = 'Rss' | 'Api';

// Mirrors Application.Models.NewsFeedSortBy - which timestamp the News Feed page's infinite
// scroll is ordered by.
export type NewsFeedSortBy = 'PublishedAt' | 'CrawledAt';

// Mirrors Application.Models.NewsFeedSortDirection - which way NewsFeedSortBy orders the feed.
export type NewsFeedSortDirection = 'Descending' | 'Ascending';

export interface NewsArticle {
  id: string;
  provider: string;
  sourceType: ArticleSourceType;
  feedName: string;
  category: string;
  title: string;
  summary: string | null;
  content: string | null;
  url: string;
  author: string | null;
  language: string;
  country: string;
  imageUrl: string | null;
  publishedAt: string | null;
  crawledAt: string;
  updatedAt: string;
  tags: string[];
}
