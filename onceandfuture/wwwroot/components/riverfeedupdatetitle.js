import React from 'react';
import { UPDATE_TITLE_FONT_SIZE, RIVER_COLUMN_BACKGROUND_COLOR } from './style';
import RiverLink from './riverlink';
import RelTime from './reltime';
import { StickyTitle } from './sticky';

const RiverFeedUpdateTitle = ({update}) => {
  const style = {
    fontSize: UPDATE_TITLE_FONT_SIZE,
    background: RIVER_COLUMN_BACKGROUND_COLOR,
  };
  return <div>
    <hr />
    <StickyTitle style={style}>
      <div style={{float: 'right'}}>Updated <RelTime time={update.whenLastUpdate} /></div>
      <RiverLink href={update.websiteUrl}>
        {update.feedTitle}
      </RiverLink>
      <div style={{float: 'clear', paddingBottom: 10,}} />
    </StickyTitle>
  </div>;
};

export default RiverFeedUpdateTitle;
