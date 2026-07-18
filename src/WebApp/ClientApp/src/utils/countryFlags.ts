// Mirrors the country `Name` strings under NewsCrawler:Countries / NewsApiCrawler:Countries in
// src/NewsCrawler.appsettings.json - keep in sync by hand when a new country is added there.
// "International" (a pseudo-country for sources that aren't tied to one nation, e.g. UN News,
// Google Fact Check) has no ISO code and deliberately has no entry here.
const COUNTRY_ISO_CODES: Record<string, string> = {
  India: 'IN',
  'United States': 'US',
  'United Kingdom': 'GB',
  Canada: 'CA',
  Australia: 'AU',
  Germany: 'DE',
  France: 'FR',
  Japan: 'JP',
  'South Korea': 'KR',
  Singapore: 'SG',
  China: 'CN',
  Indonesia: 'ID',
  Thailand: 'TH',
  Qatar: 'QA',
  Israel: 'IL',
  Mexico: 'MX',
  Turkey: 'TR',
  Ukraine: 'UA',
  Russia: 'RU',
  'South Africa': 'ZA',
  Brazil: 'BR',
  Italy: 'IT',
  Spain: 'ES',
  Netherlands: 'NL',
  Sweden: 'SE',
  Norway: 'NO',
  Finland: 'FI',
  Belgium: 'BE',
  Switzerland: 'CH',
  Austria: 'AT',
  Ireland: 'IE',
  Denmark: 'DK',
  'New Zealand': 'NZ',
  Poland: 'PL',
  'Czech Republic': 'CZ',
  Romania: 'RO',
  Hungary: 'HU',
  Greece: 'GR',
  Portugal: 'PT',
  Malaysia: 'MY',
  Vietnam: 'VN',
  Philippines: 'PH',
  Pakistan: 'PK',
  Bangladesh: 'BD',
  Nepal: 'NP',
  'Sri Lanka': 'LK',
  Nigeria: 'NG',
  Kenya: 'KE',
  Egypt: 'EG',
  Taiwan: 'TW',
  Iran: 'IR',
  UAE: 'AE',
  'Hong Kong': 'HK',
  Argentina: 'AR',
  Colombia: 'CO',
  Venezuela: 'VE',
  Myanmar: 'MM',
  Peru: 'PE',
  Morocco: 'MA',
  Algeria: 'DZ',
  Ghana: 'GH',
  Lebanon: 'LB',
  Oman: 'OM',
  Jordan: 'JO',
};

// Regional Indicator Symbol letters run from U+1F1E6 ('A') - offsetting each ASCII letter's code
// point by the same delta reproduces the two-letter ISO code as its flag emoji glyph, so this
// needs no image assets or network requests to render (unlike the provider logos, which do fetch
// favicons - this is deliberately different since a country's flag isn't fetchable from a "domain").
function isoToFlagEmoji(iso2: string): string {
  return String.fromCodePoint(...[...iso2.toUpperCase()].map((char) => 127397 + char.charCodeAt(0)));
}

export function getCountryFlagEmoji(countryName: string): string | undefined {
  const iso = COUNTRY_ISO_CODES[countryName];
  return iso ? isoToFlagEmoji(iso) : undefined;
}
