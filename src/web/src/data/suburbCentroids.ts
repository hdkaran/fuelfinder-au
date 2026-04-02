export interface SuburbCentroid {
  slug: string;
  displayName: string;
  state: string;
  postcode: string;
  lat: number;
  lng: number;
}

/** Static lookup of ~50 key Australian suburbs for SEO landing pages. */
export const suburbCentroids: SuburbCentroid[] = [
  // NSW
  { slug: 'sydney',          displayName: 'Sydney',          state: 'NSW', postcode: '2000', lat: -33.8688, lng: 151.2093 },
  { slug: 'parramatta',      displayName: 'Parramatta',      state: 'NSW', postcode: '2150', lat: -33.8150, lng: 151.0011 },
  { slug: 'newcastle',       displayName: 'Newcastle',       state: 'NSW', postcode: '2300', lat: -32.9283, lng: 151.7817 },
  { slug: 'wollongong',      displayName: 'Wollongong',      state: 'NSW', postcode: '2500', lat: -34.4278, lng: 150.8931 },
  { slug: 'penrith',         displayName: 'Penrith',         state: 'NSW', postcode: '2750', lat: -33.7512, lng: 150.6942 },
  { slug: 'blacktown',       displayName: 'Blacktown',       state: 'NSW', postcode: '2148', lat: -33.7688, lng: 150.9060 },
  { slug: 'liverpool',       displayName: 'Liverpool',       state: 'NSW', postcode: '2170', lat: -33.9213, lng: 150.9238 },
  { slug: 'campbelltown',    displayName: 'Campbelltown',    state: 'NSW', postcode: '2560', lat: -34.0657, lng: 150.8141 },
  { slug: 'gosford',         displayName: 'Gosford',         state: 'NSW', postcode: '2250', lat: -33.4281, lng: 151.3416 },
  { slug: 'maitland',        displayName: 'Maitland',        state: 'NSW', postcode: '2320', lat: -32.7335, lng: 151.5565 },

  // VIC
  { slug: 'melbourne',       displayName: 'Melbourne',       state: 'VIC', postcode: '3000', lat: -37.8136, lng: 144.9631 },
  { slug: 'geelong',         displayName: 'Geelong',         state: 'VIC', postcode: '3220', lat: -38.1499, lng: 144.3617 },
  { slug: 'ballarat',        displayName: 'Ballarat',        state: 'VIC', postcode: '3350', lat: -37.5622, lng: 143.8503 },
  { slug: 'bendigo',         displayName: 'Bendigo',         state: 'VIC', postcode: '3550', lat: -36.7570, lng: 144.2794 },
  { slug: 'frankston',       displayName: 'Frankston',       state: 'VIC', postcode: '3199', lat: -38.1440, lng: 145.1262 },
  { slug: 'dandenong',       displayName: 'Dandenong',       state: 'VIC', postcode: '3175', lat: -37.9872, lng: 145.2145 },
  { slug: 'ringwood',        displayName: 'Ringwood',        state: 'VIC', postcode: '3134', lat: -37.8163, lng: 145.2285 },
  { slug: 'sunshine',        displayName: 'Sunshine',        state: 'VIC', postcode: '3020', lat: -37.7884, lng: 144.8312 },

  // QLD
  { slug: 'brisbane',        displayName: 'Brisbane',        state: 'QLD', postcode: '4000', lat: -27.4698, lng: 153.0251 },
  { slug: 'gold-coast',      displayName: 'Gold Coast',      state: 'QLD', postcode: '4217', lat: -28.0167, lng: 153.4000 },
  { slug: 'sunshine-coast',  displayName: 'Sunshine Coast',  state: 'QLD', postcode: '4558', lat: -26.6500, lng: 153.0667 },
  { slug: 'townsville',      displayName: 'Townsville',      state: 'QLD', postcode: '4810', lat: -19.2590, lng: 146.8169 },
  { slug: 'cairns',          displayName: 'Cairns',          state: 'QLD', postcode: '4870', lat: -16.9203, lng: 145.7710 },
  { slug: 'toowoomba',       displayName: 'Toowoomba',       state: 'QLD', postcode: '4350', lat: -27.5598, lng: 151.9507 },
  { slug: 'ipswich',         displayName: 'Ipswich',         state: 'QLD', postcode: '4305', lat: -27.6167, lng: 152.7667 },
  { slug: 'mackay',          displayName: 'Mackay',          state: 'QLD', postcode: '4740', lat: -21.1411, lng: 149.1861 },
  { slug: 'rockhampton',     displayName: 'Rockhampton',     state: 'QLD', postcode: '4700', lat: -23.3791, lng: 150.5100 },

  // WA
  { slug: 'perth',           displayName: 'Perth',           state: 'WA',  postcode: '6000', lat: -31.9505, lng: 115.8605 },
  { slug: 'mandurah',        displayName: 'Mandurah',        state: 'WA',  postcode: '6210', lat: -32.5271, lng: 115.7228 },
  { slug: 'joondalup',       displayName: 'Joondalup',       state: 'WA',  postcode: '6027', lat: -31.7442, lng: 115.7661 },
  { slug: 'fremantle',       displayName: 'Fremantle',       state: 'WA',  postcode: '6160', lat: -32.0569, lng: 115.7439 },
  { slug: 'rockingham',      displayName: 'Rockingham',      state: 'WA',  postcode: '6168', lat: -32.2782, lng: 115.7278 },
  { slug: 'bunbury',         displayName: 'Bunbury',         state: 'WA',  postcode: '6230', lat: -33.3271, lng: 115.6414 },

  // SA
  { slug: 'adelaide',        displayName: 'Adelaide',        state: 'SA',  postcode: '5000', lat: -34.9285, lng: 138.6007 },
  { slug: 'glenelg',         displayName: 'Glenelg',         state: 'SA',  postcode: '5045', lat: -34.9800, lng: 138.5161 },
  { slug: 'elizabeth',       displayName: 'Elizabeth',       state: 'SA',  postcode: '5112', lat: -34.7167, lng: 138.6833 },
  { slug: 'mount-gambier',   displayName: 'Mount Gambier',   state: 'SA',  postcode: '5290', lat: -37.8311, lng: 140.7826 },
  { slug: 'whyalla',         displayName: 'Whyalla',         state: 'SA',  postcode: '5600', lat: -33.0333, lng: 137.5667 },

  // ACT
  { slug: 'canberra',        displayName: 'Canberra',        state: 'ACT', postcode: '2600', lat: -35.2809, lng: 149.1300 },
  { slug: 'belconnen',       displayName: 'Belconnen',       state: 'ACT', postcode: '2617', lat: -35.2356, lng: 149.0665 },
  { slug: 'tuggeranong',     displayName: 'Tuggeranong',     state: 'ACT', postcode: '2900', lat: -35.4244, lng: 149.0683 },

  // TAS
  { slug: 'hobart',          displayName: 'Hobart',          state: 'TAS', postcode: '7000', lat: -42.8821, lng: 147.3272 },
  { slug: 'launceston',      displayName: 'Launceston',      state: 'TAS', postcode: '7250', lat: -41.4332, lng: 147.1441 },
  { slug: 'devonport',       displayName: 'Devonport',       state: 'TAS', postcode: '7310', lat: -41.1796, lng: 146.3635 },

  // NT
  { slug: 'darwin',          displayName: 'Darwin',          state: 'NT',  postcode: '0800', lat: -12.4634, lng: 130.8456 },
  { slug: 'alice-springs',   displayName: 'Alice Springs',   state: 'NT',  postcode: '0870', lat: -23.6980, lng: 133.8807 },
  { slug: 'palmerston',      displayName: 'Palmerston',      state: 'NT',  postcode: '0830', lat: -12.4939, lng: 130.9741 },
];

/** Build a lookup by "state/slug" key for O(1) access in route params. */
export const suburbByStateSlug: Map<string, SuburbCentroid> = new Map(
  suburbCentroids.map((c) => [`${c.state.toLowerCase()}/${c.slug}`, c]),
);
