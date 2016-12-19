import React from 'react';
import { FULL_IMAGE_WIDTH } from './style';
import { make_full_url } from '../util';
import RiverLink from './riverlink';

const RiverItemThumbnail = ({item, mode = 'auto'}) => {
  let thumb = item.thumbnail;
  if (thumb) {
    let imgstyle = {
      width: 100,
      height: 100,
      marginTop: 10,
      marginLeft: 3,
      marginRight: 3,
      marginBottom: 3,
    };
    if ((mode === 'text') ||
       (mode === 'auto' && (item.body || '').length > 100)) {
      imgstyle.float = 'right';
      imgstyle.width = 100;
      imgstyle.height = 100;
    } else {
      imgstyle.width = FULL_IMAGE_WIDTH;
      imgstyle.height = FULL_IMAGE_WIDTH;
    }

    return (
      <RiverLink href={item.link}>
        <img style={imgstyle} src={make_full_url(thumb.url)} />
      </RiverLink>
    );
  } else {
    return <span />;
  }
}

export default RiverItemThumbnail;
