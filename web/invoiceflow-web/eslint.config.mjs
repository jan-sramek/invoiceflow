import angular from '@angular-eslint/eslint-plugin';
import angularTemplate from '@angular-eslint/eslint-plugin-template';
import templateParser from '@angular-eslint/template-parser';
import sheriff from '@softarc/eslint-plugin-sheriff';
import tseslint from 'typescript-eslint';

/** Turns the named rules of a plugin on at the given level; unknown names fail loudly instead of being ignored. */
function enable(prefix, plugin, names, level = 'error') {
  return Object.fromEntries(
    names.map((name) => {
      if (!(name in plugin.rules)) {
        throw new Error(`${prefix}/${name} does not exist in the installed plugin`);
      }
      return [`${prefix}/${name}`, level];
    }),
  );
}

export default tseslint.config(
  {
    ignores: [
      'dist/**',
      '.angular/**',
      'node_modules/**',
      'coverage/**',
      'out-tsc/**',
      'src/app/shared/api/schema.d.ts',
    ],
  },

  // TypeScript
  {
    files: ['**/*.ts'],
    extends: [...tseslint.configs.recommended, ...tseslint.configs.stylistic],
    plugins: { '@angular-eslint': angular },
    rules: {
      ...enable('@angular-eslint', angular, [
        'contextual-lifecycle',
        'no-empty-lifecycle-method',
        'no-input-rename',
        'no-inputs-metadata-property',
        'no-output-native',
        'no-output-on-prefix',
        'no-output-rename',
        'no-outputs-metadata-property',
        'no-queries-metadata-property',
        'prefer-inject',
        'prefer-standalone',
        'use-lifecycle-interface',
        'use-pipe-transform-interface',
      ]),
      ...enable('@angular-eslint', angular, ['prefer-on-push-component-change-detection'], 'warn'),
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix: ['app', 'if'], style: 'kebab-case' },
      ],
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: ['app', 'if'], style: 'camelCase' },
      ],
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
      '@typescript-eslint/no-explicit-any': 'error',
      // Angular style: classes and DI are fine as written.
      '@typescript-eslint/consistent-type-definitions': 'off',
      '@typescript-eslint/no-extraneous-class': 'off',
      '@typescript-eslint/class-literal-property-style': 'off',
    },
  },

  // Specs may use non-null assertions and unused destructuring to drop a key.
  {
    files: ['**/*.spec.ts'],
    rules: { '@typescript-eslint/no-non-null-assertion': 'off' },
  },

  // Templates
  {
    files: ['**/*.html'],
    languageOptions: { parser: templateParser },
    plugins: { '@angular-eslint/template': angularTemplate },
    rules: enable('@angular-eslint/template', angularTemplate, [
      'banana-in-box',
      'eqeqeq',
      'no-negated-async',
      'no-duplicate-attributes',
      'prefer-control-flow',
      // Accessibility
      'alt-text',
      'click-events-have-key-events',
      'elements-content',
      'interactive-supports-focus',
      'label-has-associated-control',
      'no-autofocus',
      'role-has-required-aria',
      'table-scope',
      'valid-aria',
    ]),
  },

  // Module boundaries (see sheriff.config.ts)
  sheriff.configs.all,
);
