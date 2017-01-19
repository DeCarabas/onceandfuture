import React from 'react';
import { ITEM_TITLE_FONT_SIZE } from './style';
import RiverLink from './riverlink';

const RiverItemTitle = ({item}) => {
  const style = {
    fontSize: ITEM_TITLE_FONT_SIZE,
    overflowWrap: "break-word",
  };
  let titleText = item.title || item.pubDate;
  if (titleText.length > 280) {
    titleText = titleText.substring(0, 280);
  }
  return (
    <RiverLink href={item.link}>
      <span style={style}>{ titleText }</span>
    </RiverLink>
  );
}

export default RiverItemTitle;
