import React from 'react';
import { ITEM_TITLE_FONT_SIZE } from './style';
import RiverLink from './riverlink';

const RiverItemTitle = ({item}) => {
  const style = {
    fontSize: ITEM_TITLE_FONT_SIZE,
  };
  const titleText = item.title || item.pubDate;
  return (
    <RiverLink href={item.link}>
      <span style={style}>{ titleText }</span>
    </RiverLink>
  );
}

export default RiverItemTitle;
