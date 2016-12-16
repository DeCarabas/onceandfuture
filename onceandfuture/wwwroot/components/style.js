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
export const UPDATE_TITLE_FONT_SIZE = 12;

export const ICON_FONT_SIZE = 18;

// ---- Sizes

// ---------------------------------------------------------------------- -
//    RIVER                                                     | R | U | 36px  SIZE_BANNER_HEIGHT
// ---------------------------------------------------------------------- -
// =====================================                                  10px  SIZE_PROGRESS_HEIGHT
//  10px              10px              10px                              10px  SIZE_SPACER_HEIGHT
// ||     360px      ||      360px     ||
//   +--------------+  +--------------+                                         SIZE_COLUMN_TOP
//   |              |  |              |
export const SIZE_BANNER_HEIGHT = 36;                  // A nice height.
export const SIZE_BUTTON_HEIGHT = SIZE_BANNER_HEIGHT;  // Buttons fill the banner top-to-bottom.
export const SIZE_BUTTON_WIDTH = SIZE_BUTTON_HEIGHT;   // Buttons are square.

export const SIZE_LARGE_BUTTON_HEIGHT = SIZE_BUTTON_HEIGHT * 2;
export const SIZE_LARGE_BUTTON_WIDTH = SIZE_BUTTON_WIDTH * 2;

export const SIZE_PROGRESS_HEIGHT = 10;
export const SIZE_SPACER_HEIGHT = 10;
export const SIZE_SPACER_WIDTH = 10;

export const SIZE_PAGE_HEADER = SIZE_BANNER_HEIGHT + SIZE_PROGRESS_HEIGHT;

export const SIZE_COLUMN_TOP = SIZE_PAGE_HEADER + SIZE_SPACER_HEIGHT;
export const SIZE_COLUMN_WIDTH = 360;

export const SIZE_FULL_IMAGE_GUTTER = 24;
export const SIZE_FULL_IMAGE_WIDTH = SIZE_COLUMN_WIDTH - (2 * SIZE_FULL_IMAGE_GUTTER);

// ---- Layers

export const Z_INDEX_BASE   = 0;
export const Z_INDEX_BANNER = 10;

// KILL THESE
export const COLUMNWIDTH = 350;
export const FULL_IMAGE_WIDTH = 300;
export const COLUMNSPACER = 10;
export const PROGRESS_HEIGHT = 10;

// ---- Default Styles

export const DEFAULT_LINK_STYLE = {
  color: DEFAULT_LINK_COLOR,
  textDecoration: 'initial',
};

// TODO: THIS IS GARBAGE.
export const BUTTON_STYLE = {
  fontSize: ICON_FONT_SIZE,
  float: 'right',
  paddingTop: 8,
  paddingRight: COLUMNSPACER,
  cursor: 'pointer',
};
