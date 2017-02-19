import React from 'react';
import RiverSet from './riverset';
import {
  APP_TEXT_COLOR,
  SANS_FONTS,
  TEXT_FONT_SIZE,
  APP_BACKGROUND_COLOR,

  SIZE_SCREEN_WIDTH,
  SIZE_SCREEN_HEIGHT,
} from './style';

// We have this thing because we need the background color to scroll. I think normally we'd just put the background
// color on the <body> element? But because we use this whole style in JS thing I don't want to have two definitions
// for colors. (I suppose I could add one of those fancy tools that actually mixes real CSS and JS, but not yet.)
const AppBackground = () => {
  const bgstyle = {
    backgroundColor: APP_BACKGROUND_COLOR,
    position: 'fixed',
    top: 0,
    left: 0,
    width: SIZE_SCREEN_WIDTH,
    height: SIZE_SCREEN_HEIGHT,
  };
  return <div style={bgstyle} />;
}

const AppRoot = () => {
  const appstyle = {
    color: APP_TEXT_COLOR,
    fontFamily: SANS_FONTS,
    fontSize: TEXT_FONT_SIZE,
    height: SIZE_SCREEN_HEIGHT,
  };
  return <div style={appstyle} >
    <AppBackground />
    <RiverSet />
  </div>;
};

export default AppRoot;
