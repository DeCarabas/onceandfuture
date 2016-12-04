import React from 'react';
import RiverSet from './riverset';
import {
  APP_TEXT_COLOR,
  SANS_FONTS,
  TEXT_FONT_SIZE,
  APP_BACKGROUND_COLOR,
} from './style';

// AppRoot
//
const AppRoot = () => {
  const appstyle = {
    color: APP_TEXT_COLOR,
    fontFamily: SANS_FONTS,
    fontSize: TEXT_FONT_SIZE,
    height: '100%',
  };
  const bgstyle = {
    backgroundColor: APP_BACKGROUND_COLOR,
    position: 'fixed',
    top: 0,
    left: 0,
    width: '100%',
    height: '100%',
  };
  return <div style={appstyle} >
    <div style={bgstyle} />
    <RiverSet />
  </div>;
};

export default AppRoot;
