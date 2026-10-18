import { sameTag, SheriffConfig, anyTag } from '@softarc/sheriff-core';

/**
 * Module boundaries (ADR-0017). Every folder with an `index.ts` is a module; other modules may only import
 * through that barrel, and only what the rules below allow.
 *
 *   app root          -> core, shared, features        (wires everything together)
 *   core/<area>       -> core, shared                  (app-wide singletons: http, i18n, config, layout, ...)
 *   shared/<area>     -> shared                        (stateless building blocks; knows nothing of core or features)
 *   features/<f>/...  -> core, shared, the same feature (never another feature)
 *
 * Inside a feature, slices depend one way only:  feature-* -> ui, data-access, util;  data-access -> util;  ui -> util.
 */
export const config: SheriffConfig = {
  version: 1,
  entryPoints: { app: 'src/main.ts' },
  enableBarrelLess: false,
  autoTagging: true,
  modules: {
    'src/app/core/<area>': ['layer:core'],
    'src/app/shared/<area>': ['layer:shared'],
    'src/app/features/<feature>/<slice>': ['layer:feature', 'feature:<feature>', 'slice:<slice>'],
  },
  depRules: {
    root: anyTag,
    // Layer barrels (core/index.ts, features/invoices/index.ts, ...) only re-export their children.
    noTag: anyTag,

    'layer:core': ['layer:core', 'layer:shared'],
    'layer:shared': ['layer:shared'],

    // What a feature may use is decided per feature (below) and per slice (further below).
    'layer:feature': anyTag,

    // A feature may use core and shared, and itself; never another feature.
    'feature:*': [sameTag, 'layer:core', 'layer:shared'],

    // Slices of a feature: pages -> ui, data-access, util;  data-access -> util;  ui -> util.
    'slice:util': ['slice:util', 'layer:core', 'layer:shared'],
    'slice:ui': ['slice:util', 'layer:core', 'layer:shared'],
    'slice:data-access': ['slice:util', 'layer:core', 'layer:shared'],
    'slice:feature-*': ['slice:ui', 'slice:data-access', 'slice:util', 'layer:core', 'layer:shared'],
  },
};
