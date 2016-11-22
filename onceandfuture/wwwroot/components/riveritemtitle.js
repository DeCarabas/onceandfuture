var React = require('react'); // N.B. Still need this because JSX.
import { DEFAULT_LINK_STYLE, ITEM_TITLE_FONT_SIZE } from './style'
import RiverLink from './riverlink'

const RiverItemTitle = ({item}) => {
  const style = Object.assign({}, DEFAULT_LINK_STYLE, {
    fontSize: ITEM_TITLE_FONT_SIZE,
  });
  let titleText = item.title || item.pubDate;
  return (
    <RiverLink href={item.link}>
      <span style={style}>{ titleText }</span>
    </RiverLink>
  );
}

export default RiverItemTitle
