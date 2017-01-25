// ---- Palette
// http://paletton.com/#uid=12-0u0kleqtbzEKgVuIpcmGtdhZ
export const COLOR_VERY_DARK = '#42800B';
export const COLOR_DARK = '#5EA222';
export const COLOR_BASE = '#7ABD3F';
export const COLOR_LIGHT = '#9EDB67';
export const COLOR_VERY_LIGHT = '#BFEC97';

export const COLOR_BLACK = '#222';
export const COLOR_DARK_GREY = '#444';
export const COLOR_VERY_LIGHT_GREY = '#EEE';
export const COLOR_LIGHT_GREY = '#BBB';

export const APP_BACKGROUND_COLOR = COLOR_VERY_LIGHT;
export const APP_TEXT_COLOR = COLOR_DARK_GREY;
export const DEFAULT_LINK_COLOR = COLOR_BLACK;
export const RIVER_COLUMN_BACKGROUND_COLOR = COLOR_VERY_LIGHT_GREY;
export const RIVER_TITLE_BACKGROUND_COLOR = COLOR_BASE;

// ---- Fonts

export const SANS_FONTS = [
  'AvenirNext-Medium',
  'HelveticaNeue-Medium',
  'Helvetica Neue',
  'Helvetica',
  'Arial',
  'sans-serif',
];

export const RIVER_TITLE_FONT_SIZE = 24;
export const ITEM_TITLE_FONT_SIZE = 18;
export const TEXT_FONT_SIZE = 12;
export const UPDATE_TITLE_FONT_SIZE = 14;

export const ICON_FONT_SIZE = 18;

// ---- Sizes
// Generally, sizes and positions should be defined here, not computed
// locally in the component.

// ---------------------------------------------------------------------- -
//    RIVER                                                     | R | U | 36px  SIZE_BANNER_HEIGHT
// ---------------------------------------------------------------------- -
// =====================================                                  10px  SIZE_PROGRESS_HEIGHT
//  10px              10px (SIZE_SPACER_WIDTH)                            10px  SIZE_SPACER_HEIGHT
// ||     360px      ||      360px (SIZE_COLUMN_WIDTH)
//   +--------------+  +--------------+                                         SIZE_COLUMN_TOP
//   |              |  |              |
export const SIZE_BANNER_HEIGHT = 36;                  // A nice height.
export const SIZE_BUTTON_HEIGHT = SIZE_BANNER_HEIGHT;  // Buttons fill the banner top-to-bottom.
export const SIZE_BUTTON_WIDTH = SIZE_BUTTON_HEIGHT;   // Buttons are square.

export const SIZE_PROGRESS_HEIGHT = 10;
export const SIZE_SPACER_HEIGHT = 10;
export const SIZE_SPACER_WIDTH = 10;

export const SIZE_PAGE_HEADER = SIZE_BANNER_HEIGHT + SIZE_PROGRESS_HEIGHT;

export const SIZE_COLUMN_TOP = SIZE_PAGE_HEADER + SIZE_SPACER_HEIGHT;
export const SIZE_COLUMN_WIDTH = 300; // 360 is so nice but doesn't fit on an iPhone 5.

export const SIZE_FULL_IMAGE_GUTTER = 24;
export const SIZE_FULL_IMAGE_WIDTH = SIZE_COLUMN_WIDTH - (2 * SIZE_FULL_IMAGE_GUTTER);

// Buttons
export const SIZE_BUTTON_PADDING = 8;
export const SIZE_BUTTON_FONT = SIZE_BUTTON_WIDTH - (2 * SIZE_BUTTON_PADDING);

// Banner (up at top of the app)
export const SIZE_BANNER_TITLE_FONT = 24;
export const SIZE_BANNER_TITLE_PADDING_HORIZONTAL = SIZE_SPACER_WIDTH;
export const SIZE_BANNER_TITLE_PADDING_VERTICAL = (SIZE_BANNER_HEIGHT - SIZE_BANNER_TITLE_FONT) / 2;
export const SIZE_ANNOUNCER_HEIGHT = SIZE_BANNER_HEIGHT;
export const SIZE_ANNOUNCER_FONT = 12;
export const SIZE_ANNOUNCER_PADDING_VERTICAL = (SIZE_BANNER_HEIGHT - SIZE_ANNOUNCER_FONT) / 2;


// (River column stuff)
//   /-------------------------------\
//   |                               | SIZE_RIVER_TITLE_TOP_SPACER
//   +-------------------------------+
//   | = Main                      S | SIZE_RIVER_TITLE_HEIGHT
//   +-------------------------------+
//   |                               |
export const SIZE_RIVER_TITLE_TOP_SPACER = SIZE_SPACER_HEIGHT;
export const SIZE_RIVER_TITLE_HEIGHT = SIZE_BANNER_HEIGHT;
export const SIZE_RIVER_TITLE_FONT = 24;
export const SIZE_RIVER_TITLE_PADDING_HORIZONTAL = SIZE_BUTTON_WIDTH;
export const SIZE_RIVER_TITLE_PADDING_VERTICAL = (SIZE_RIVER_TITLE_HEIGHT - SIZE_RIVER_TITLE_FONT) / 2;

export const SIZE_RIVER_MODAL_TOP = SIZE_RIVER_TITLE_TOP_SPACER + SIZE_RIVER_TITLE_HEIGHT;


// ---- Layers
export const Z_INDEX_BASE             = 0;
export const Z_INDEX_BANNER           = 100;
export const Z_INDEX_SETTINGS         = 300;
export const Z_INDEX_ACCOUNT_SETTINGS = 400;
export const Z_INDEX_ACCOUNT_MENU     = 410;
export const Z_INDEX_TOOLTIP          = 900;

// ---- Useful shorthand

export const RIVER_SETTINGS_BASE_STYLE = {
  backgroundColor: COLOR_VERY_LIGHT,
  border: '1px solid ' + COLOR_VERY_DARK,
  padding: SIZE_SPACER_WIDTH,
  zIndex: Z_INDEX_SETTINGS,
};


// KILL THESE
export const COLUMNSPACER = 10;
