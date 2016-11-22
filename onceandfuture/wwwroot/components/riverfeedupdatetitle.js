var React = require('react'); // N.B. Still need this because JSX.
import { UPDATE_TITLE_FONT_SIZE } from './style'
import RiverLink from './riverlink'

const RiverFeedUpdateTitle = ({update}) => {
  const style = {
    fontSize: UPDATE_TITLE_FONT_SIZE,
  };
  return <div style={style}>
    <hr />
    <div style={{float: 'right'}}>{update.whenLastUpdate}</div>
    <RiverLink href={update.websiteUrl}>
      {update.feedTitle}
    </RiverLink>
    <div style={{float: 'clear', marginBottom: 10,}} />
  </div>;
};

export default RiverFeedUpdateTitle;
