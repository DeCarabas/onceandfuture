var React = require('react'); // N.B. Still need this because JSX.
import RiverItemTitle from './riveritemtitle'
import RiverItemThumbnail from './riveritemthumbnail'

const RiverItem = ({item, mode = 'auto'}) => {
  let style = {
    border: "1px solid AAA",
    overflow: 'auto',
  };

  return(
    <div style={style}>
      <RiverItemTitle item={item} />
      <div style={{float: 'clear'}} />
      <RiverItemThumbnail item={item} mode={mode} />
      <p>{ item.body }</p>
      <div style={{float: 'clear'}} />
    </div>
  );
};

export default RiverItem;
