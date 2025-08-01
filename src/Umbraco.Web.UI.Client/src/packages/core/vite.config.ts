import { defineConfig } from 'vite';
import { rmSync } from 'fs';
import { getDefaultConfig } from '../../vite-config-base';

const dist = '../../../dist-cms/packages/core';

// delete the unbundled dist folder
rmSync(dist, { recursive: true, force: true });

export default defineConfig({
	...getDefaultConfig({
		dist,
		base: '/umbraco/backoffice/packages/core',
		entry: {
			'action/index': './action/index.ts',
			'audit-log/index': './audit-log/index.ts',
			'auth/index': './auth/index.ts',
			'backend-api/index': './backend-api/index.ts',
			'cache/index': './cache/index.ts',
			'collection/index': './collection/index.ts',
			'components/index': './components/index.ts',
			'const/index': './const/index.ts',
			'culture/index': './culture/index.ts',
			'dashboard/index': './dashboard/index.ts',
			'debug/index': './debug/index.ts',
			'entity-action/index': './entity-action/index.ts',
			'entity-bulk-action/index': './entity-bulk-action/index.ts',
			'entity-create-option-action/index': './entity-create-option-action/index.ts',
			'entity/index': './entity/index.ts',
			'entity-item/index': './entity-item/index.ts',
			'entry-point': 'entry-point.ts',
			'event/index': './event/index.ts',
			'extension-registry/index': './extension-registry/index.ts',
			'http-client/index': './http-client/index.ts',
			'icon-registry/index': './icon-registry/index.ts',
			'id/index': './id/index.ts',
			'lit-element/index': './lit-element/index.ts',
			'localization/index': './localization/index.ts',
			'menu/index': './menu/index.ts',
			'modal/index': './modal/index.ts',
			'models/index': './models/index.ts',
			'notification/index': './notification/index.ts',
			'object-type/index': './object-type/index.ts',
			'picker-input/index': './picker-input/index.ts',
			'picker/index': './picker/index.ts',
			'property-action/index': './property-action/index.ts',
			'property-editor/index': './property-editor/index.ts',
			'property/index': './property/index.ts',
			'recycle-bin/index': './recycle-bin/index.ts',
			'repository/index': './repository/index.ts',
			'resources/index': './resources/index.ts',
			'router/index': './router/index.ts',
			'section/index': './section/index.ts',
			'server/index': './server/index.ts',
			'server-file-system/index': './server-file-system/index.ts',
			'sorter/index': './sorter/index.ts',
			'store/index': './store/index.ts',
			'style/index': './style/index.ts',
			'temporary-file/index': './temporary-file/index.ts',
			'themes/index': './themes/index.ts',
			'tree/index': './tree/index.ts',
			'utils/index': './utils/index.ts',
			'validation/index': './validation/index.ts',
			'variant/index': './variant/index.ts',
			'workspace/index': './workspace/index.ts',
			manifests: 'manifests.ts',
		},
	}),
});
